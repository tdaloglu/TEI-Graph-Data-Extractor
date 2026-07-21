using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services
{
    public class GraphDataService :IGraphDataService
    {
        private readonly Stack<List<DataPoint>> _undoHistory = new Stack<List<DataPoint>>();

        private List<DataPoint> _currentStrokePoints = new List<DataPoint>();

        public void BeginNewStroke()
        {
            _currentStrokePoints = new List<DataPoint>();
        }

        public void RegisterPoint(DataPoint point, ObservableCollection<DataPoint> liveList)
        {
            _currentStrokePoints.Add(point);
            liveList.Add(point);
        }

        public void EndCurrentStroke()
        {
            if (_currentStrokePoints.Count > 0)
            {
                _undoHistory.Push(_currentStrokePoints);
            }
        }

        public bool UndoLastStroke(ObservableCollection<DataPoint> liveList)
        {
            if (_undoHistory.Count == 0) return false;

            var lastStroke = _undoHistory.Pop();

            foreach (var point in lastStroke)
            {
                liveList.Remove(point);
            }

            return true;
        }

        public List<DataPoint> FilterPointsByZ(IEnumerable<DataPoint> allPoints, double zValue)
        {
            return allPoints.Where(p => Math.Abs(p.ZValue - zValue) < 0.0001).ToList();
        }

        public void ClearHistory()
        {
            _undoHistory.Clear();
            _currentStrokePoints.Clear();
        }
    }
}