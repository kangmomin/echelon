import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';

const url = process.env.ECHELON_WEB_URL || 'http://127.0.0.1:18080/';
const prefix = process.env.ECHELON_WEB_PREFIX || 'build/validation/web';
const chromePath = process.env.CHROME || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const messages = [];
const errors = [];
const knownUnsupportedShaders = new Set([
    'Hidden/CoreSRP/CoreCopy',
    'Hidden/Universal Render Pipeline/StencilDitherMaskSeed',
    'Hidden/Universal/HDRDebugView',
]);

function collectShaderWarnings() {
    return messages.filter(text => [...knownUnsupportedShaders].some(name =>
        text.startsWith(name + ' shader is not supported on this GPU')));
}

let nextId = 1;
const pending = new Map();
let socket;
let chrome;
let browserLog = '';
let userDataDir;

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function waitFor(check, timeout = 90000) {
    const start = Date.now();
    while (Date.now() - start < timeout) {
        if (check()) return;
        await delay(250);
    }
    throw new Error('Timed out: ' + messages.join(' | '));
}

async function connect() {
    let targets;
    for (let i = 0; i < 600; i++) {
        try {
            const port = fs.readFileSync(userDataDir + '/DevToolsActivePort', 'utf8').split(/\r?\n/)[0].trim();
            const response = await fetch(`http://127.0.0.1:${port}/json/list`);
            if (response.ok) {
                const pages = await response.json();
                if (pages.some(target => target.type === 'page')) {
                    targets = pages;
                    break;
                }
            }
        } catch {}
        await delay(250);
    }
    const page = targets?.find(target => target.type === 'page');
    if (!page) throw new Error('Chrome page target unavailable\n' + browserLog);
    socket = new WebSocket(page.webSocketDebuggerUrl);
    socket.addEventListener('message', event => {
        const message = JSON.parse(event.data);
        if (message.id && pending.has(message.id)) {
            const { resolve, reject } = pending.get(message.id);
            pending.delete(message.id);
            if (message.error) reject(new Error(JSON.stringify(message.error)));
            else resolve(message.result || {});
        }
        if (message.method === 'Runtime.consoleAPICalled') {
            const text = message.params.args.map(arg => arg.value ?? arg.description ?? '').join(' ');
            messages.push(text);
            if (message.params.type === 'error') errors.push(text);
        }
        if (message.method === 'Runtime.exceptionThrown') {
            const text = JSON.stringify(message.params.exceptionDetails);
            messages.push(text);
            errors.push(text);
        }
        if (message.method === 'Log.entryAdded') {
            const entry = message.params.entry;
            messages.push(entry.text);
            if (entry.level === 'error') errors.push(entry.text);
        }
    });
    await new Promise((resolve, reject) => {
        socket.addEventListener('open', resolve, { once: true });
        socket.addEventListener('error', reject, { once: true });
    });
}

function call(method, params = {}) {
    return new Promise((resolve, reject) => {
        const id = nextId++;
        pending.set(id, { resolve, reject });
        socket.send(JSON.stringify({ id, method, params }));
        setTimeout(() => {
            if (pending.delete(id)) reject(new Error('CDP timeout: ' + method));
        }, 30000).unref();
    });
}

async function capture(name) {
    const result = await call('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync(`${prefix}_${name}.png`, Buffer.from(result.data, 'base64'));
}

async function click(x, y) {
    await call('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y });
    await call('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
    await delay(120);
    await call('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
}

async function main() {
    const angle = process.env.CHROME_ANGLE || 'swiftshader';
    userDataDir = fs.mkdtempSync(path.resolve('build/validation/chrome-profile-'));
    chrome = spawn(chromePath, [
        '--headless=new', '--remote-debugging-port=0', '--remote-allow-origins=*',
        '--user-data-dir=' + userDataDir, '--no-first-run', '--no-default-browser-check',
        '--disable-extensions', '--disable-background-networking', '--disable-dev-shm-usage',
        '--use-gl=angle', '--use-angle=' + angle, '--enable-gpu', '--enable-unsafe-swiftshader', '--enable-logging=stderr',
        '--window-size=1600,900', '--hide-scrollbars', '--mute-audio', 'about:blank',
    ], { stdio: ['ignore', 'pipe', 'pipe'], env: { ...process.env, MSYS2_ARG_CONV_EXCL: '*' } });
    chrome.on('error', error => { browserLog += String(error); });
    chrome.stdout.on('data', chunk => { browserLog += chunk.toString(); });
    chrome.stderr.on('data', chunk => { browserLog += chunk.toString(); });
    await connect();
    await call('Page.enable');
    await call('Runtime.enable');
    await call('Log.enable');
    await call('Emulation.setDeviceMetricsOverride', {
        width: 1600, height: 900, deviceScaleFactor: 1, mobile: false,
    });
    await call('Page.navigate', { url });
    await waitFor(() => messages.some(text => text.includes('[data] elements 57')));
    await delay(2500);
    await capture('menu');
    await call('Runtime.evaluate', { expression: 'document.querySelector("#unity-canvas").focus()' });
    await click(360, 540); // 메뉴의 "샌드박스" 버튼
    await waitFor(() => messages.some(text => text.includes('[sim] battle start')), 30000);
    await delay(3000);
    await capture('sandbox');
    const warnings = collectShaderWarnings();
    for (const text of messages) {
        if (text.includes('shader is not supported on this GPU') && !warnings.includes(text))
            errors.push(text);
    }
    if (errors.length) throw new Error(errors.join('\n'));
    fs.writeFileSync(prefix + '_console.json', JSON.stringify({ messages, errors, warnings }, null, 2));
    console.log(`[web-smoke] OK menu and sandbox; known_shader_warnings=${warnings.length}`);
}

try {
    await main();
} catch (error) {
    console.error(error.stack || error);
    if (socket) {
        try {
            const state = await call('Runtime.evaluate', {
                expression: '({url: location.href, ready: document.readyState, text: document.body.innerText})',
                returnByValue: true,
            });
            browserLog += '\nPage state: ' + JSON.stringify(state.result?.value);
            await capture('failure');
        } catch (captureError) {
            browserLog += '\nCapture failed: ' + String(captureError);
        }
    }
    fs.writeFileSync(prefix + '_console.json', JSON.stringify({
        messages, errors, warnings: collectShaderWarnings(), browserLog,
    }, null, 2));
    process.exitCode = 1;
} finally {
    if (socket) socket.close();
    if (chrome && chrome.exitCode === null) chrome.kill();
}
