// Tiny test runner: each suite file registers named tests with T.Add; run `tools/tests/run.sh [filter]`.
using System;
using System.Collections.Generic;
using System.IO;

public static class T
{
    public static readonly List<(string suite, string name, Action run)> All = new List<(string, string, Action)>();
    public static string Root;
    public static void Add(string suite, string name, Action run) => All.Add((suite, name, run));
    public static void True(bool ok, string msg) { if (!ok) throw new Exception(msg); }
    public static void Eq<X>(X expected, X actual, string what = "") { if (!Equals(expected, actual)) throw new Exception(what + " expected '" + expected + "' but got '" + actual + "'"); }
    public static string Path(string rel) => System.IO.Path.Combine(Root, rel);
}

public static class Program
{
    public static int Main(string[] args)
    {
        string d = AppContext.BaseDirectory;
        while (d != null && !File.Exists(System.IO.Path.Combine(d, "CLAUDE.md"))) d = System.IO.Path.GetDirectoryName(d);
        T.Root = d ?? Directory.GetCurrentDirectory();
        Suites.Register();
        string filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
        int pass = 0, fail = 0;
        foreach (var t in T.All)
        {
            if (filter != null && !(t.suite + "/" + t.name).ToLowerInvariant().Contains(filter)) continue;
            try { t.run(); pass++; }
            catch (Exception e) { fail++; Console.WriteLine("FAIL " + t.suite + " / " + t.name + ": " + (e.InnerException ?? e).Message); }
        }
        Console.WriteLine(fail == 0 ? "all " + pass + " tests passed" : fail + " failed, " + pass + " passed");
        return fail == 0 ? 0 : 1;
    }
}
