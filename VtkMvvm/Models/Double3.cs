namespace VtkMvvm.Models;

public readonly record struct Double3(double X, double Y, double Z)
{
    public static Double3 Zero => new(0, 0, 0);
}