using System.Collections.ObjectModel;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services;

public interface IGraphDataService
{
    void BeginNewStroke();
    void EndCurrentStroke();
    void RegisterPoint(DataPoint point, ObservableCollection<DataPoint> liveDataPoints);
    void ClearHistory();
    bool UndoLastStroke(ObservableCollection<DataPoint> liveDataPoints);
}