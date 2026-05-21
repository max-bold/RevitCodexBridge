var uidoc = app.ActiveUIDocument;
var doc = uidoc?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

Func<double, double> mm = v => UnitUtils.ConvertFromInternalUnits(v, UnitTypeId.Millimeters);
Func<XYZ, object> pt = p => new {
    xMm = Math.Round(mm(p.X), 3),
    yMm = Math.Round(mm(p.Y), 3),
    zMm = Math.Round(mm(p.Z), 3)
};

var selectedIds = uidoc.Selection.GetElementIds().ToList();
var selectedSweeps = selectedIds
    .Select(id => doc.GetElement(id))
    .OfType<Sweep>()
    .OrderBy(e => e.Id.IntegerValue)
    .ToList();

var allSweeps = new FilteredElementCollector(doc)
    .OfClass(typeof(Sweep))
    .Cast<Sweep>()
    .OrderBy(e => e.Id.IntegerValue)
    .ToList();

var targetSweeps = selectedSweeps.Count > 0 ? selectedSweeps : allSweeps;

var results = new List<object>();
foreach (var sweep in targetSweeps)
{
    var path = sweep.Path3d;
    var segments = new List<object>();
    double totalFt = 0.0;
    int segmentIndex = 0;

    if (path != null)
    {
        for (int loopIndex = 0; loopIndex < path.NumCurveLoops; loopIndex++)
        {
            CurveArray loop = path.get_CurveLoop(loopIndex);
            foreach (Curve c in loop)
            {
                segmentIndex++;
                double lenFt = c.Length;
                totalFt += lenFt;
                segments.Add(new {
                    index = segmentIndex,
                    loopIndex,
                    type = c.GetType().Name,
                    lengthMm = Math.Round(mm(lenFt), 3),
                    start = pt(c.GetEndPoint(0)),
                    end = pt(c.GetEndPoint(1))
                });
            }
        }
    }

    results.Add(new {
        id = sweep.Id.IntegerValue,
        uniqueId = sweep.UniqueId,
        name = sweep.Name,
        pathLoopCount = path == null ? 0 : path.NumCurveLoops,
        segmentCount = segmentIndex,
        totalFeet = Math.Round(totalFt, 9),
        totalMillimeters = Math.Round(mm(totalFt), 3),
        totalMeters = Math.Round(UnitUtils.ConvertFromInternalUnits(totalFt, UnitTypeId.Meters), 6),
        segments
    });
}

return new {
    ok = true,
    document = doc.Title,
    selectedElementCount = selectedIds.Count,
    selectedSweepCount = selectedSweeps.Count,
    totalSweepCount = allSweeps.Count,
    measuredMode = selectedSweeps.Count > 0 ? "selected-sweeps"
        : (allSweeps.Count == 1 ? "single-sweep-fallback" : "all-sweeps-selection-not-visible"),
    sweeps = results
};
