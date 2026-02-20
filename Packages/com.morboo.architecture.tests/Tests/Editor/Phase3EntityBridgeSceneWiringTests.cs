using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class Phase3EntityBridgeSceneWiringTests
{
    const string LevelScenePath = "Assets/Game/Scenes/Level.unity";
    const string BridgeScriptGuid = "bed87d494ddd4eda95bffa54c5b0f561";

    static readonly Regex BridgeBlockRegex = new Regex(
        @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*" + BridgeScriptGuid + @",\s*type:\s*3\}" +
        @"[\s\S]*?unitManager:\s*\{fileID:\s*(?<unit>-?\d+)\}" +
        @"[\s\S]*?enemyManager:\s*\{fileID:\s*(?<enemy>-?\d+)\}",
        RegexOptions.Compiled);

    [Test]
    public void LevelScene_HasGameEntityBackboneBridge_WithManagerRefs()
    {
        Assert.That(File.Exists(LevelScenePath), Is.True, $"Missing scene: {LevelScenePath}");

        string source = File.ReadAllText(LevelScenePath);
        Match match = BridgeBlockRegex.Match(source);

        Assert.That(match.Success, Is.True,
            "Could not find GameEntityBackboneBridge component block in Level scene.");

        int unitFileId = int.Parse(match.Groups["unit"].Value);
        int enemyFileId = int.Parse(match.Groups["enemy"].Value);

        Assert.That(unitFileId, Is.Not.EqualTo(0), "Bridge unitManager reference is not assigned.");
        Assert.That(enemyFileId, Is.Not.EqualTo(0), "Bridge enemyManager reference is not assigned.");
    }
}
