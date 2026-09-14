using System.Text;
using App.GitHealth.Api.Hosting.Desktop.Bridge;

namespace App.GitHealth.Api.Tests.Hosting.Desktop.Bridge;

public sealed class DesktopFileSaverTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"githealth-saver-{Guid.NewGuid():N}");

    [Fact]
    public void SaveWritesThePayloadUnderTheNameThePageAsked()
    {
        var path = DesktopFileSaver.Save(_directory, "branches.csv", Encode("name;age\n"));

        Assert.Equal(Path.Combine(_directory, "branches.csv"), path);
        Assert.Equal("name;age\n", File.ReadAllText(path!));
    }

    [Fact]
    public void SaveNumbersANameAlreadyTakenRatherThanOverwritingIt()
    {
        DesktopFileSaver.Save(_directory, "branches.csv", Encode("first"));

        var path = DesktopFileSaver.Save(_directory, "branches.csv", Encode("second"));

        Assert.Equal(Path.Combine(_directory, "branches (2).csv"), path);
        Assert.Equal("first", File.ReadAllText(Path.Combine(_directory, "branches.csv")));
        Assert.Equal("second", File.ReadAllText(path!));
    }

    [Theory]
    [InlineData("../escaped.csv")]
    [InlineData("..\\escaped.csv")]
    [InlineData("nested/escaped.csv")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void SaveRefusesANameThatCouldWriteOutsideTheFolder(string? fileName)
    {
        Assert.Null(DesktopFileSaver.Save(_directory, fileName, Encode("payload")));
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData("not base64 at all")]
    [InlineData(null)]
    public void SaveRefusesAPayloadItCannotDecode(string? contents)
    {
        Assert.Null(DesktopFileSaver.Save(_directory, "branches.csv", contents));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void SaveKeepsTheBytesIntactForABinaryFile()
    {
        var bytes = new byte[] { 0x53, 0x51, 0x4c, 0x69, 0x74, 0x65, 0x00, 0xff };

        var path = DesktopFileSaver.Save(
            _directory,
            "githealth-backup.db",
            Convert.ToBase64String(bytes));

        Assert.Equal(bytes, File.ReadAllBytes(path!));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string Encode(string contents) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(contents));
}
