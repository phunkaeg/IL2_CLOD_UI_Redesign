namespace PlaneLoadoutWpfTest;

/// <summary>Represents a bullet type in the ammo belt selector.</summary>
public sealed class BulletOption
{
    public string Name      { get; }
    public string ImagePath { get; }
    public BulletOption(string name, string imagePath) { Name = name; ImagePath = imagePath; }
}
