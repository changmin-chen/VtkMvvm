namespace UnitTest;


[CollectionDefinition("VTK")] public sealed class VtkCollection : ICollectionFixture<VtkFixture> { }

public sealed class VtkFixture : IDisposable
{
    public VtkFixture()
    {
     
    }
    public void Dispose() { }
}