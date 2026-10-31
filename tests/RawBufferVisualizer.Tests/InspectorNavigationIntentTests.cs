using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class InspectorNavigationIntentTests
    {
        public static void RunAll()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { CheckBindings(); }
                catch (Exception ex) { failure = ex; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Headless inspector binding checks exceeded ten seconds.");
            if (failure != null)
                throw new InvalidOperationException("Inspector intent checks failed.", failure);
        }

        private static void CheckBindings()
        {
            var sourcePath = Path.Combine(Environment.CurrentDirectory, "src",
                "RawBufferVisualizer.VisualStudio.Vssdk", "RawBufferToolWindowControl.xaml");
            var xaml = XDocument.Load(sourcePath);
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace definitions = "http://schemas.microsoft.com/winfx/2006/xaml";
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var directory = Path.GetFullPath(Path.Combine(tempRoot,
                "RawBufferVisualizer-InspectorIntent-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            var checkedBindings = 0;
            try
            {
                foreach (var width in new[] { 640, 1000 })
                foreach (var section in new[] { "Pixel", "Histogram", "Format", "Compare", "Diagnostics" })
                {
                    var definition = xaml.Descendants(presentation + "ToggleButton").Single(element =>
                        (string?)element.Attribute(definitions + "Name") == "Inspector" + section + "Button");
                    // Use the production interaction bindings, without its visual template or a desktop window.
                    var isolatedButton = new XElement(presentation + "ToggleButton",
                        new XAttribute("xmlns", presentation.NamespaceName),
                        new XAttribute("IsChecked", (string)definition.Attribute("IsChecked")!),
                        new XAttribute("Command", (string)definition.Attribute("Command")!),
                        new XAttribute("CommandParameter", (string)definition.Attribute("CommandParameter")!));
                    var button = (ToggleButton)XamlReader.Parse(isolatedButton.ToString());
                    var navigation = new InspectorNavigationViewModel();
                    navigation.SetWidth(width);
                    navigation.Show(section);
                    var store = new AutomaticInspectionPreferencesStore(Path.Combine(directory,
                        width + "-" + section + ".json"));
                    Action save = () =>
                    {
                        Require(store.TrySave(new AutomaticInspectionPreferences
                        {
                            AutoScanOnBreak = false,
                            DockedInspectorOpen = navigation.DockedOpen,
                            OverlayInspectorOpen = navigation.OverlayOpen
                        }, out var error), error);
                    };
                    save();
                    var saves = 0;
                    navigation.OpenStateChanged += (_, _) => { save(); saves++; };
                    button.DataContext = navigation;
                    DrainBindings();
                    Require(button.IsChecked == true, "Restored selection did not reach the rail button.");
                    Require(BindingOperations.GetBinding(button, ToggleButton.IsCheckedProperty)?.Mode == BindingMode.OneWay,
                        "Selection display must not write back into saved navigation state.");
                    Require(button.Command == navigation.ToggleSectionCommand && (string)button.CommandParameter == section,
                        "The actual rail binding must execute the matching section command.");

                    var previousFile = File.ReadAllText(store.Path);
                    button.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                    button.DataContext = null;
                    button.DataContext = navigation;
                    DrainBindings();
                    Require(navigation.IsOpen && saves == 0 && File.ReadAllText(store.Path) == previousFile,
                        "Passive checked-state reset or binding teardown overwrote the user's open choice.");

                    var otherMode = navigation.IsNarrow ? navigation.DockedOpen : navigation.OverlayOpen;
                    Click(button);
                    Require(!navigation.IsOpen && button.IsChecked == false && saves == 1,
                        "An actual repeat click must close once and persist once.");
                    Click(button);
                    Require(navigation.IsOpen && button.IsChecked == true && saves == 2,
                        "An actual reopening click must open once and persist once.");
                    Require((navigation.IsNarrow ? navigation.DockedOpen : navigation.OverlayOpen) == otherMode,
                        "A click changed the other layout mode's remembered state.");

                    previousFile = File.ReadAllText(store.Path);
                    navigation.SetWidth(width == 640 ? 1000 : 640);
                    navigation.SetWidth(width);
                    button.DataContext = null;
                    button.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                    DrainBindings();
                    Require(navigation.IsOpen && saves == 2 && File.ReadAllText(store.Path) == previousFile,
                        "Responsive placement or final binding teardown changed the last explicit choice.");
                    var reopened = new AutomaticInspectionPreferencesStore(store.Path).Load();
                    var restored = new InspectorNavigationViewModel();
                    restored.RestoreOpenState(reopened.DockedInspectorOpen, reopened.OverlayInspectorOpen);
                    restored.SetWidth(width);
                    Require(restored.IsOpen && !reopened.AutoScanOnBreak,
                        "Recreation lost the last click or the existing Auto Inspect preference.");
                    checkedBindings++;
                }
                Console.WriteLine("Inspector intent: " + checkedBindings
                    + " production section/mode bindings passed passive-reset, click and teardown/recreation checks.");
            }
            finally
            {
                if (!directory.StartsWith(tempRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(directory).StartsWith("RawBufferVisualizer-InspectorIntent-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected inspector intent cleanup path.");
                Directory.Delete(directory, true);
            }
        }

        private static void Click(ToggleButton button)
        {
            // Invoke WPF's real toggle-then-command sequence, rather than only raising a Click event.
            typeof(ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(button, null);
            DrainBindings();
        }

        private static void DrainBindings() =>
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
