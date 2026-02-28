using System.Windows;

namespace VeryActiveDebugProfile.Services;

public interface IWindowPlacementService
{
    void Restore(Window window);
    void Save(Window window);
}