using System;
using System.IO;
using Echelon.Core;
using Echelon.Unity.App;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.Input
{
    public abstract class BattleInputTestFixture
    {
        private static DataCatalog _catalog;

        protected static DataCatalog Catalog => _catalog ??= LoadCatalog();
        protected Battle Battle { get; private set; }
        protected BattleInputController Controller { get; private set; }
        protected GameObject ControllerObject { get; private set; }
        protected double Now { get; set; }

        [SetUp]
        public void SetUpInput()
        {
            Now = 0.0;
            Battle = BattleSetup.Sandbox(Catalog);
            ControllerObject = new GameObject("BattleInputControllerTests");
            Controller = ControllerObject.AddComponent<BattleInputController>();
            Controller.Configure(new BattleCommands(Battle), (Vector2 screen, out Vector2 target) =>
            {
                target = screen;
                return true;
            });
            Controller.TimeSource = () => Now;
            Controller.UiKeyboardFocusProbe = () => false;
            Controller.PointerOverUiProbe = _ => false;
        }

        [TearDown]
        public void TearDownInput()
        {
            if (ControllerObject != null)
                UnityEngine.Object.DestroyImmediate(ControllerObject);
            Battle?.Dispose();
        }

        private static DataCatalog LoadCatalog()
        {
            string overrideRoot = Environment.GetEnvironmentVariable("ECHELON_REPOSITORY_ROOT");
            string root = !string.IsNullOrEmpty(overrideRoot)
                ? overrideRoot
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            DataCatalog catalog = DataLoader.LoadCatalog(Path.Combine(root, "data"));
            Assert.That(catalog.Errors, Is.Empty, string.Join("\n", catalog.Errors));
            return catalog;
        }
    }
}
