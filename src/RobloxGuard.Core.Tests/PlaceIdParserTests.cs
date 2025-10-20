using RobloxGuard.Core;
using Xunit;

namespace RobloxGuard.Core.Tests;

public class PlaceIdParserTests
{
    #region Protocol URI Samples (from docs/parsing_tests.md)

    [Theory]
    [InlineData("roblox://experiences/start?placeId=1818&launchData=x", 1818)]
    [InlineData("roblox://placeId=1537690962/", 1537690962)]
    [InlineData("roblox-player:1+launchmode:app+...PlaceLauncher.ashx?request=RequestGame&placeId=1416690850&userId=-1", 1416690850)]
    public void Extract_ProtocolUris_ReturnsCorrectPlaceId(string uri, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(uri);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion

    #region Client CLI Samples (from docs/parsing_tests.md)

    [Theory]
    [InlineData("RobloxPlayerBeta.exe --id 519015469", 519015469)]
    [InlineData("RobloxPlayerBeta.exe --play -j https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&placeId=1416690850&userId=-1 -a https://... -t <token>", 1416690850)]
    public void Extract_ClientCommandLines_ReturnsCorrectPlaceId(string commandLine, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(commandLine);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Extract_NoPlaceId_ReturnsNull()
    {
        // Arrange
        var input = "roblox://some/random/path";

        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_EmptyString_ReturnsNull()
    {
        // Act
        var result = PlaceIdParser.Extract("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_NullString_ReturnsNull()
    {
        // Act
        var result = PlaceIdParser.Extract(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_MultiplePlaceIds_ReturnsFirstOccurrence()
    {
        // Arrange
        var input = "roblox://placeId=1111&placeId=2222";

        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1111, result.Value);
    }

    [Theory]
    [InlineData("roblox://PlaceLauncher.Ashx?placeId=12345", 12345)] // Mixed case
    [InlineData("roblox://placelauncher.ashx?PLACEID=67890", 67890)] // All caps
    [InlineData("RobloxPlayerBeta.exe --ID 99999", 99999)] // Uppercase --ID
    public void Extract_MixedCasing_IsCaseInsensitive(string input, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion

    #region Real-World Examples

    [Theory]
    [InlineData("roblox://experiences/start?placeId=920587237&gameInstanceId=abc123", 920587237)]
    [InlineData("roblox-player:1+launchmode:play+gameinfo:https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=12345&placeId=606849621&isPlayTogetherGame=false", 606849621)]
    [InlineData("\"C:\\Program Files (x86)\\Roblox\\Versions\\version-abc\\RobloxPlayerBeta.exe\" --play -j https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&placeId=2753915549&userId=123456789", 2753915549)]
    public void Extract_RealWorldScenarios_ParsesCorrectly(string input, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion

    #region ExtractAll Tests

    [Fact]
    public void ExtractAll_MultiplePlaceIds_ReturnsAllUnique()
    {
        // Arrange
        var input = "roblox://placeId=1111&test=123&placeId=2222";

        // Act
        var results = PlaceIdParser.ExtractAll(input);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(1111, results);
        Assert.Contains(2222, results);
    }

    [Fact]
    public void ExtractAll_NoPlaceIds_ReturnsEmptyList()
    {
        // Arrange
        var input = "roblox://no/place/id/here";

        // Act
        var results = PlaceIdParser.ExtractAll(input);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Whitespace and Special Characters

    [Theory]
    [InlineData("  roblox://placeId=1818  ", 1818)] // Leading/trailing spaces
    [InlineData("roblox://placeId=1818\n", 1818)] // Newline
    [InlineData("roblox://placeId=1818\r\n", 1818)] // CRLF
    [InlineData("RobloxPlayerBeta.exe  --id  12345", 12345)] // Multiple spaces
    public void Extract_WhitespaceVariations_ParsesCorrectly(string input, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion

    #region Large Numbers

    [Theory]
    [InlineData("roblox://placeId=9999999999", 9999999999)] // Large place ID
    [InlineData("RobloxPlayerBeta.exe --id 1234567890123", 1234567890123)] // Very large
    public void Extract_LargeNumbers_ParsesCorrectly(string input, long expectedPlaceId)
    {
        // Act
        var result = PlaceIdParser.Extract(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPlaceId, result.Value);
    }

    #endregion
}
