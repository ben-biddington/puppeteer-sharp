using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Nunit;

namespace PuppeteerSharp.Tests.CDPSessionTests.Screencasting;

sealed class ScreencastingTests : PuppeteerPageBaseTest
{
    [Test, PuppeteerTest("StartScreencast.spec", "Page.startScreencast", "should send events")]
    public async Task ShouldSendEvents()
    {
        var client = await Page.CreateCDPSessionAsync();
        var events = new List<MessageEventArgs>();

        client.MessageReceived += (_, e) =>
        {
            if (e.MessageID == "Page.screencastFrame")
            {
                events.Add(e);
            }
        };

        await client.SendAsync("Network.enable");
        await client.SendAsync("Page.startScreencast");

        await Page.GoToAsync(TestConstants.EmptyPage);

        await client.SendAsync("Page.stopScreencast");
        await client.DetachAsync();

        foreach (var e in events)
        {
            Console.WriteLine(e.MessageID);
            Console.WriteLine(e.MessageData);
        }

        Assert.That(events, Is.Not.Empty);
    }
}
