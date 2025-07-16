using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetCoreServer.extensions;

public static class SpanExtensions
{
    private static readonly ReadOnlyMemory<byte> HttpNewLineBreak = "\r\n\r\n"u8.ToArray();

    private static readonly FieldInfo f_List_items =
        typeof(List<byte>).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo f_Queue_array = typeof(Queue<byte>)
        .GetField("_array", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo f_Queue_head = typeof(Queue<byte>)
        .GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo f_Queue_tail = typeof(Queue<byte>)
        .GetField("_tail", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <inheritdoc cref="HttpNewLineIndex(System.Span{byte})"/>
    public static int HttpNewLineIndex(this ReadOnlySpan<byte> bytes) => bytes.LastIndexOf(HttpNewLineBreak.Span);
    
    /// <summary>
    /// Searches last index of HTTP new line break (CR-LF-CR-LF). 
    /// </summary>
    /// <param name="bytes">Bytes data span</param>
    /// <returns>-1 if line break was not found, zero-based index otherwise</returns>
    public static int HttpNewLineIndex(this Span<byte> bytes) => bytes.LastIndexOf(HttpNewLineBreak.Span);

    /// <summary>
    /// Convert to <see cref="Span{T}"/><br/><br/>
    /// <b>WARNING!</b><br/>
    /// Utilize reflection, use with wisdom!
    /// </summary>
    /// <param name="bytes"></param>
    public static Span<byte> AsSpan(this List<byte> bytes)
    {
        return (f_List_items.GetValue(bytes) as byte[]).AsSpan(0, bytes.Count);
    }

    /// <summary>
    /// Convert to <see cref="Span{T}"/><br/><br/>
    /// <b>WARNING!</b><br/>
    /// Utilize reflection, use with wisdom!
    /// </summary>
    /// <remarks>
    /// <see cref="Queue{T}"/> is not a really great way to retreive <see cref="Span{T}"/>. It
    /// </remarks>
    /// <param name="bytes">Bytes data</param>
    public static Span<byte> AsSpan(this Queue<byte> bytes)
    {
        var array = (f_Queue_array.GetValue(bytes) as byte[]);
        var head = (int)(f_Queue_head.GetValue(bytes));
        var tail = (int)(f_Queue_tail.GetValue(bytes));

        // easy condition
        if (head < tail)
        {
            return array.AsSpan(head, bytes.Count);
        }

        return bytes.AsEnumerable().AsSpanDangerous();
    }

    /// <summary>
    /// Convert to <see cref="Span{T}"/><br/>
    /// </summary>
    /// <remarks>Creates new array which may be very expensive</remarks>
    /// <param name="bytes">Bytes data</param>
    public static Span<byte> AsSpanDangerous(this IEnumerable<byte> bytes) => bytes.ToArray().AsSpan();
}