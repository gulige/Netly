﻿using System.Security.Cryptography.X509Certificates;

namespace test;
using Netly.Core;

public class BufferParserTest
{
    [Fact]
    public void EndToEnd()
    {
        byte[] data = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        byte[] sendBuffer = BufferParser.SetPrefix(ref data);

        List<byte[]> receivedMessages = BufferParser.GetMessages(ref sendBuffer);

        int _size = receivedMessages.Count;

        Assert.Equal(1, _size);
        Assert.Equal(10, receivedMessages[0].Length);
    }

    [Fact]
    public void EndToEndMultMessage()
    {
        byte[] data1 = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        byte[] data2 = { 0, 1, 2, 3, 4, };
        byte[] data3 = { 0, 1, 2, 3, 4, 5, 6, 7 };

        byte[] sendBuffer1 = BufferParser.SetPrefix(ref data1);
        byte[] sendBuffer2 = BufferParser.SetPrefix(ref data2);
        byte[] sendBuffer3 = BufferParser.SetPrefix(ref data3);

        List<byte[]> buffers = new List<byte[]> { sendBuffer1, sendBuffer2, sendBuffer3 };

        var result = buffers.SelectMany(x => x).ToArray();

        List<byte[]> receivedMessages = BufferParser.GetMessages(ref result);

        int _size = receivedMessages.Count;

        Assert.Equal(3, _size);
        Assert.Equal(9, receivedMessages[0].Length);
        Assert.Equal(5, receivedMessages[1].Length);
        Assert.Equal(8, receivedMessages[2].Length);

        Assert.Equal(data1, receivedMessages[0]);
        Assert.Equal(data2, receivedMessages[1]);
        Assert.Equal(data3, receivedMessages[2]);
    }
}

