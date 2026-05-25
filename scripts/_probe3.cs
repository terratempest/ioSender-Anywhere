using System; using System.Linq; using System.Reflection;
class P { static void Main(string[] a) {
  var asm = Assembly.LoadFrom(a[0]);
  var load = asm.GetType("CNC.Localization.CsvLocaleLoader").GetMethod("LoadCulture");
  var cat = (System.Collections.Generic.IReadOnlyDictionary<string,string>)load.Invoke(null, new object[]{a[1],a[2]});
  var probes = new[] {
    "CNC.Controls.Probing.probingview.str_probeWarning",
    "ioSender.workspace.editor_mdiTouch",
    "ioSender.workspace.editor_program",
    "CNC.Controls.Avalonia.libstrings.str_appconfCreate"
  };
  foreach (var p in probes) Console.WriteLine(p + " => " + (cat.TryGetValue(p, out var v) ? v : "MISSING"));
}}
