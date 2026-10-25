from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class UnityWebRequestHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        name = str(path)
        if name.endswith(".br"):
            name = name[:-3]
        if name.endswith(".wasm"):
            return "application/wasm"
        return super().guess_type(name)

    def end_headers(self):
        if self.path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve a Unity WebGL build with its required response headers")
    parser.add_argument("--root", default="build/unity/web")
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    if not (root / "index.html").is_file():
        raise SystemExit(f"Unity WebGL index.html not found: {root}")
    handler = partial(UnityWebRequestHandler, directory=str(root))
    with ThreadingHTTPServer((args.bind, args.port), handler) as server:
        print(f"Serving {root} at http://{args.bind}:{args.port}/", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
