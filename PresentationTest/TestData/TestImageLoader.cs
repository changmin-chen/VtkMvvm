using System.IO;
using System.Reflection;
using itk.simple;
using Image = itk.simple.Image;

namespace PresentationTest.TestData;

public static class TestImageLoader
{
    public static Image LoadEmbeddedTestImage(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Format: [ProjectNamespace].[FolderName].[FileName]
        // Example: "MyProject.TestData.test_mri.nii"
        var fullResourceName = $"{assembly.GetName().Name}.TestData.{resourceName}";

        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
            throw new FileNotFoundException($"Resource {fullResourceName} not found");

        // Create a temporary file to read with SimpleITK
        var tempFile = Path.GetTempFileName() + ".nii";
        using (var fileStream = File.Create(tempFile))
        {
            stream.CopyTo(fileStream);
        }

        var image = SimpleITK.ReadImage(tempFile);

        // Clean up temp file
        try
        {
            File.Delete(tempFile);
        }
        catch
        {
            /* ignore errors */
        }

        return image;
    }
}