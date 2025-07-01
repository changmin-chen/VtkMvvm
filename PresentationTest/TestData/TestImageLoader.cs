using itk.simple;
using Kitware.VTK;
using Image = itk.simple.Image;

namespace PresentationTest.TestData;

public enum VtkScalarType
{
    UnsignedChar = 3, // byte
    Short = 4,
    UnsignedShort = 5,
    Float = 10
}

public static class TestImageLoader
{
    public static vtkImageData ReadNifti(string path)
    {
        Image itkImage = SimpleITK.ReadImage(path);
        itkImage = SimpleITK.DICOMOrient(itkImage, "LPS");   // RAS -> LPS
        var vtkImage = itkImage.ToVtkIgnoreDirection();
        
        itkImage.Dispose();
        return vtkImage;
    }

    private static vtkImageData ToVtkIgnoreDirection(this Image itkImage)
    {
        VectorDouble? spacing = itkImage.GetSpacing();
        VectorDouble? origin = itkImage.GetOrigin();
        VectorUInt32? sizes = itkImage.GetSize();

        // Initialize vtk image
        vtkImageData? vtkImage = vtkImageData.New();
        vtkImage.SetOrigin(origin[0], origin[1], origin[2]);
        vtkImage.SetSpacing(spacing[0], spacing[1], spacing[2]);
        vtkImage.SetExtent(0, (int)sizes[0] - 1, 0, (int)sizes[1] - 1, 0, (int)sizes[2] - 1);
        vtkImage.SetWholeExtent(0, (int)sizes[0] - 1, 0, (int)sizes[1] - 1, 0, (int)sizes[2] - 1);
        vtkImage.SetNumberOfScalarComponents((int)itkImage.GetNumberOfComponentsPerPixel());


        // Copy memory
        int bytesAlloc = itkImage.GetTotalBytesAlloc();
        VtkScalarType scalarType = itkImage.GetPixelID().ToVtkScalarType();
        vtkImage.SetScalarType((int)scalarType);
        vtkImage.AllocateScalars();
        unsafe
        {
            void* src = itkImage.GetScalarPointer().ToPointer();
            void* dest = vtkImage.GetScalarPointer().ToPointer();
            Buffer.MemoryCopy(src, dest, bytesAlloc, bytesAlloc);
        }

        vtkImage.Update();
        return vtkImage;
    }

    private static int GetBytesPerPixel(this Image itkImage)
    {
        return itkImage.GetPixelID().swigValue switch
        {
            0 or 1 => sizeof(byte), // Int8 or UInt8 -> 1 byte
            2 or 3 => sizeof(short), // Int16 or UInt16 -> 2 bytes
            4 or 8 => sizeof(float), // Int32 or Float32 -> 4 bytes
            _ => throw new NotSupportedException($"Pixel ID {itkImage.GetPixelID().swigValue} is not supported.")
        };
    }

    private static int GetTotalBytesAlloc(this Image itkImage)
    {
        int noc = (int)itkImage.GetNumberOfComponentsPerPixel();
        int npx = (int)itkImage.GetNumberOfPixels();
        return itkImage.GetBytesPerPixel() * npx * noc;
    }
}

/// <summary>
///     Helper methods to convert between ITK and VTK image
/// </summary>
internal static class ItkVtkScalarTypeHelper
{
    // Mapping for PixelIDValueEnum to Buffer Accessors
    private static readonly Dictionary<PixelIDValueEnum, Func<Image, IntPtr>> ItkBufferAccessorMapping = new()
    {
        { PixelIDValueEnum.sitkFloat32, image => image.GetBufferAsFloat() },
        { PixelIDValueEnum.sitkUInt16, image => image.GetBufferAsUInt16() },
        { PixelIDValueEnum.sitkInt16, image => image.GetBufferAsInt16() },
        { PixelIDValueEnum.sitkUInt8, image => image.GetBufferAsUInt8() }
    };

    /// <summary>
    ///     Convert VTK scalar type to ITK PixelIDValueEnum
    /// </summary>
    internal static PixelIDValueEnum ToItkScalarType(this VtkScalarType vtkType)
    {
        return vtkType switch
        {
            VtkScalarType.UnsignedChar => PixelIDValueEnum.sitkUInt8,
            VtkScalarType.UnsignedShort => PixelIDValueEnum.sitkUInt16,
            VtkScalarType.Short => PixelIDValueEnum.sitkInt16,
            VtkScalarType.Float => PixelIDValueEnum.sitkFloat32,
            _ => throw new ArgumentOutOfRangeException(nameof(vtkType), vtkType, null)
        };
    }

    /// <summary>
    ///     Converts ITK PixelIDValueEnum to VTK scalar type.
    /// </summary>
    internal static VtkScalarType ToVtkScalarType(this PixelIDValueEnum pixelId)
    {
        // Compare based on integer values or known static references
        if (pixelId == PixelIDValueEnum.sitkUInt8)
            return VtkScalarType.UnsignedChar;
        if (pixelId == PixelIDValueEnum.sitkUInt16)
            return VtkScalarType.UnsignedShort;
        if (pixelId == PixelIDValueEnum.sitkInt16)
            return VtkScalarType.Short;
        if (pixelId == PixelIDValueEnum.sitkFloat32)
            return VtkScalarType.Float;

        throw new ArgumentOutOfRangeException(nameof(pixelId), pixelId, "Unsupported PixelIDValueEnum");
    }

    // Accessing ITK image buffer
    public static IntPtr GetScalarPointer(this Image itkImage)
    {
        Func<Image, IntPtr> bufferAccessor = ItkBufferAccessorMapping[itkImage.GetPixelID()];
        return bufferAccessor(itkImage);
    }


    // Make VTK scalar type more readable
    public static VtkScalarType GetNominalScalarType(this vtkImageData vtkImage) => (VtkScalarType)vtkImage.GetScalarType(); // Make sure it is defined in the enum
}