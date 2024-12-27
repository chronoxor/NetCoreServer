using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetCoreServer.extensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable PossibleMultipleEnumeration

namespace tests;

public class ExtensionsTests
{
    private static readonly Dictionary<double, string> _sizes = new()
    {
        { Math.Pow(2, 00), "b" },
        { Math.Pow(2, 10), "KB" },
        { Math.Pow(2, 20), "MB" },
        { Math.Pow(2, 30), "GB" },
    };

    private static readonly Dictionary<double, string> _times = new()
    {
        { Math.Pow(10, 00), "ns" },
        { Math.Pow(10, 03), "μs" },
        { Math.Pow(10, 06), "mls" },
        { Math.Pow(10, 09), "sec" },
        { Math.Pow(10, 12), "min" },
    };

    private readonly ITestOutputHelper _helper;

    public ExtensionsTests(ITestOutputHelper helper)
    {
        _helper = helper;
    }

    /// <summary>
    /// Union provided data
    /// </summary>
    /// <param name="values">
    /// Data to concatenate. Supported values:<br/>
    /// <b>IEnumerable&lt;byte&gt;<br/></b>
    /// <b>IEnumerable&lt;string&gt;<br/></b>
    /// <b>IEnumerable&lt;IEnumerable&lt;byte&gt;&gt;<br/></b>
    /// <b>IEnumerable&lt;IEnumerable&lt;string&gt;&gt;<br/></b>
    /// <b>string<br/></b>
    /// </param>
    /// <returns></returns>
    private static byte[] Concat2Array(params object[] values)
    {
        var result = new List<byte>();

        foreach (var value in values)
        {
            switch (value)
            {
                case string s:
                    result.AddRange(Encoding.UTF8.GetBytes(s));
                    break;

                case IEnumerable common:
                    // byte[] and byte[][]
                    var bytes = common.OfType<byte>().Concat(common.OfType<IEnumerable<byte>>().SelectMany(x => x));
                    result.AddRange(bytes);

                    // string[] and string[][]
                    bytes = common.OfType<string>().Concat(common.OfType<IEnumerable<string>>().SelectMany(x => x))
                        .SelectMany(x => Encoding.UTF8.GetBytes(x));
                    result.AddRange(bytes);
                    break;

                default:
                    Console.WriteLine($"Warn: Value is not supported: {value.GetType()}");
                    break;
            }
        }

        return result.ToArray();
    }

    public static object[][] FindIndexData()
    {
        var httpStop = "\r\n\r\n"u8.ToArray();
        var complicatedNoNewLine =
            Enumerable.Repeat("1\r\n\r2\r\n\r3"u8.ToArray(), 20_000).SelectMany(x => x).ToArray();
        var complicated = Concat2Array(httpStop, complicatedNoNewLine);

        var sw = Stopwatch.StartNew();
        var result = new object[][]
        {
            // simple
            [httpStop, 0, "No body provided but only newline"],
            [Concat2Array("5", httpStop), 1, "body is size of one byte"],
            [
                Concat2Array("5", Enumerable.Repeat(httpStop, 10), "5"), -httpStop.Length - 1,
                "repeated newline counts from end, so we are looking for 4 index from end"
            ],

            // tricky ones
            [Concat2Array("5", httpStop, "5"), 1, "Some tricky one 1"],
            [Concat2Array("51", httpStop, "51"), 2, "Some tricky one 2"],
            [Concat2Array("513", httpStop, "523"), 3, "Some tricky one 3"],
            [Concat2Array("5\r3", httpStop, "5\r3"), 3, "Some tricky one 4"],
            [Concat2Array("5\r3\n", httpStop, "5\r3\n"), 4, "Some tricky one 5"],
            [
                Concat2Array("\r\n\r5", httpStop, "\r\n\r5"), -httpStop.Length - 2,
                "Some tricky one 6 (last symbols should extend HTTP newline break to the end of array)"
            ],
            [Concat2Array("\r\r\r\r", httpStop, "\r\r\r\r"), 4, "Some tricky one 7"],
            [Concat2Array("\n\n\n\n", httpStop, "\n\n\n\n"), 4, "Some tricky one 8"],
            [Concat2Array("\n\r\r\n", httpStop, "\n\r\r\n"), 4, "Some tricky one 9"],

            // not found
            [Concat2Array("\r\n\n\r"), null, "Valid break was not found 1"],
            [Concat2Array("\n\n"), null, "Valid break was not found 2"],
            [Concat2Array("\n\n\r\r"), null, "Valid break was not found 3"],
            [Concat2Array("\n\r\n\r"), null, "Valid break was not found 4"],

            // large calculation
            [complicated, 0, "[Array] large and fill with CR/LF symbols"],
            [complicatedNoNewLine, null, "[Array] large and fill with CR/LF symbols, newline not found"],

            [new List<byte>(complicated), 0, "[List] large and fill with CR/LF symbols"],
            [new List<byte>(complicatedNoNewLine), null, "[List] large and fill with CR/LF symbols, newline not found"],

            [new Queue<byte>(complicated), 0, "[Queue] large and fill with CR/LF symbols"],
            [
                new Queue<byte>(complicatedNoNewLine), null,
                "[Queue] large and fill with CR/LF symbols, newline not found"
            ],
        };

        sw.Stop();
        Console.WriteLine($"Data generating took: {sw.ElapsedTicks}ticks");
        return result;
    }

    [Theory]
    [MemberData(nameof(FindIndexData))]
    public void FindIndex(IEnumerable<byte> list, int? expected, string msg)
    {
        expected = expected switch
        {
            // no HTTP newline contains
            null => -1,

            // searching from the end
            < 0 => list.Count() + expected,

            // no change
            _ => expected,
        };

        Func<int> action = list switch
        {
            byte[] x => () => x.AsSpan().HttpNewLineIndex(),
            List<byte> x => () => x.AsSpan().HttpNewLineIndex(),
            Queue<byte> x => () => x.AsSpan().HttpNewLineIndex(),
            _ => () => list.ToArray().AsSpan().HttpNewLineIndex(),
        };

        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();

        _helper.WriteLine($"Calc {stopwatch.ElapsedTicks} ticks");

        if (string.IsNullOrEmpty(msg))
        {
            Assert.Equal(expected, value);
        }
        else
        {
            if (value != expected)
            {
                throw EqualException.ForMismatchedValues(expected, value, msg);
            }
        }
    }

    private static string PrettyPrint(double value, Dictionary<double, string> sizes, bool addPlus = false)
    {
        var abs = Math.Abs(value);

        var entry = sizes
            .OrderByDescending(x => x.Key)
            .FirstOrDefault(x => abs >= x.Key, _sizes.OrderBy(x => x.Key).First());

        return $"{(addPlus && value > 0 ? "+" : "")}{value / entry.Key:F} {entry.Value}";
    }

    // [Theory]
    // [InlineData(false)]
    // [InlineData(true)]
    public void CheckSpeed(bool haveBreak)
    {
        var arr = Concat2Array(haveBreak ? "\r\n\r\n" : "", Enumerable.Repeat("1\r\n\r2\r\n\r3"u8.ToArray(), 20_000));
        var list = new List<byte>(arr);
        var queue = new Queue<byte>(arr);
        var iterations = 5_000;
        var expected = haveBreak ? 0 : -1;
        Stopwatch sw;

        _helper.WriteLine($"Starting {iterations:D} iterations");

        void TestCase(Func<int> indexAction, string name)
        {
            var old = GC.GetTotalMemory(true);
            sw = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
            {
                var index = indexAction();
                Assert.Equal(expected, index);
            }

            sw.Stop();
            var current = GC.GetTotalMemory(false);
            _helper.WriteLine($"{name, 10}: {PrettyPrint(sw.Elapsed.TotalNanoseconds, _times), 15}, memory: " +
                              $"{PrettyPrint(old, _sizes), 10} -> {PrettyPrint(current, _sizes), 10}, " +
                              $"({PrettyPrint(current - old, _sizes, true)})");
        }
        
        TestCase(() => arr.AsSpan().HttpNewLineIndex(), "Array");
        TestCase(() => list.AsSpan().HttpNewLineIndex(), "List");
        TestCase(() => queue.AsSpan().HttpNewLineIndex(), "Queue");
    }
}