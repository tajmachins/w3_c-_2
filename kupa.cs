using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Task2;

public readonly record struct Reading(string Sensor, double Value, DateTime At);

public record class Data(
    double HighestValue,                        // highest recorded value across all sensors
    double LowestTemp,                          // lowest recorded temperature
    string MostReadings,                        // name of the sensor with most readings
    TimeSpan TotalDuration,                     // difference between first and last recorded reading
    IList<string> UniqueSensors,                // list of all sensor names without duplicates
    IList<(string, double)> AverageReadings,    // average reading per sensor
    IList<(string, TimeSpan)> LargestGaps,      // a largest timespan between two consecutive readings (time order) per sensor
    IList<(Reading, Reading)> SignChanges       // two consecutive readings (time order) having two differnet signs (first < second) 
);

public class SensorLog {
    private readonly List<Reading> _readings = new();
    public void Add(Reading r) { _readings.Add(r); }
    public IReadOnlyList<Reading> All => _readings;

    public SensorLog LoadFromCsv(string path) {
        bool first = true;
        foreach (var line in File.ReadLines(path)) {
            if (first) { first = false; continue; }          
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            var parts = line.Split(',');
            Add(new Reading(parts[0].Trim(), double.Parse(parts[1], CultureInfo.InvariantCulture), DateTime.Parse(parts[2], CultureInfo.InvariantCulture)));
        }
        return this;
    }
}

// =============================================================
// TODO 1
// =============================================================

public static class T1 {
    public static IEnumerable<Reading> InRange(IEnumerable<Reading> source, double min, double max) {
        foreach (var reading in source) {
            if (reading.Value >= min && reading.Value <= max) {
                yield return reading;
            }
        }
    }
}

// =============================================================
// TODO 2
// =============================================================

public static class T2 {
    public static IEnumerable<Reading> Repeat(string sensor, double value, int count, DateTime start) {
        DateTime currentAt = start;
        double currentValue = value;

        while (true) {
            yield return new Reading(sensor, currentValue, currentAt);
            currentAt = currentAt.AddMinutes(2);
            currentValue += 0.2; // 0,1 na minutę * 2 minuty = 0,2
        }
    }
}

// =============================================================
// TODO 3
// =============================================================

public class ReadingBySensorThenValue : Comparer<Reading> {
    public override int Compare(Reading x, Reading y) {
        // 1. Nazwa czujnika rosnąco
        int sensorComparison = string.Compare(x.Sensor, y.Sensor, StringComparison.Ordinal);
        if (sensorComparison != 0) {
            return sensorComparison;
        }

        // 2. Wartość malejąco
        int valueComparison = y.Value.CompareTo(x.Value);
        if (valueComparison != 0) {
            return valueComparison;
        }

        // 3. Czas rejestracji rosnąco
        return x.At.CompareTo(y.At);
    }
}

// =============================================================
// TODO 4
// =============================================================

public static class T4 {
    private class ReadingChronologicalComparer : Comparer<Reading> {
        public override int Compare(Reading x, Reading y) => x.At.CompareTo(y.At);
    }

    public static Data Summarize(IEnumerable<Reading> source) {
        double highestValue = double.MinValue;
        double lowestTemp = double.MaxValue;
        DateTime minAt = DateTime.MaxValue;
        DateTime maxAt = DateTime.MinValue;

        Dictionary<string, List<Reading>> bySensor = new();

        foreach (var r in source) {
            if (r.Value > highestValue) highestValue = r.Value;
            if (r.Sensor == "tmp" && r.Value < lowestTemp) lowestTemp = r.Value;
            if (r.At < minAt) minAt = r.At;
            if (r.At > maxAt) maxAt = r.At;

            if (!bySensor.ContainsKey(r.Sensor)) {
                bySensor[r.Sensor] = new List<Reading>();
            }
            bySensor[r.Sensor].Add(r);
        }

        TimeSpan totalDuration = maxAt - minAt;

        // Czujnik z największą liczbą odczytów
        string mostReadings = "";
        int maxCount = -1;
        foreach (var (sensor, list) in bySensor) {
            if (list.Count > maxCount) {
                maxCount = list.Count;
                mostReadings = sensor;
            }
        }

        // Posortowana lista unikalnych czujników
        List<string> uniqueSensors = new(bySensor.Keys);
        uniqueSensors.Sort();

        // Średnie odczyty per czujnik
        List<(string, double)> averageReadings = new();
        foreach (var sensor in uniqueSensors) {
            var list = bySensor[sensor];
            double sum = 0;
            foreach (var r in list) {
                sum += r.Value;
            }
            averageReadings.Add((sensor, sum / list.Count));
        }

        // Największe luki i zmiany znaków w porządku chronologicznym
        List<(string, TimeSpan)> largestGaps = new();
        List<(Reading, Reading)> signChanges = new();
        var chronoComparer = new ReadingChronologicalComparer();

        foreach (var sensor in uniqueSensors) {
            var list = bySensor[sensor];
            list.Sort(chronoComparer);

            TimeSpan maxGap = TimeSpan.Zero;
            for (int i = 0; i < list.Count - 1; i++) {
                var prev = list[i];
                var next = list[i + 1];

                TimeSpan gap = next.At - prev.At;
                if (gap > maxGap) {
                    maxGap = gap;
                }

                if ((prev.Value > 0 && next.Value < 0) || (prev.Value < 0 && next.Value > 0)) {
                    signChanges.Add((prev, next));
                }
            }
            largestGaps.Add((sensor, maxGap));
        }

        return new Data(
            highestValue,
            lowestTemp,
            mostReadings,
            totalDuration,
            uniqueSensors,
            averageReadings,
            largestGaps,
            signChanges
        );
    }
}

// =============================================================
// TODO 5
// =============================================================

public static class T5 {
    public static string Classify(IReadOnlyList<Reading> readings) => readings switch {
        [] => "empty",
        [_] => "single",
        [{ Value: > 100 }, ..] => "starts-high",
        [.., { Value: < 0 }] => "ends-low",
        [{ Value: var v1 }, { Value: var v2 }] when v1 < v2 => "pair-asc",
        [{ Value: var v1 }, { Value: var v2 }] when v1 > v2 => "pair-desc",
        [{ Value: var v1 }, { Value: var v2 }] when v1 == v2 => "pair-eq",
        _ => "other"
    };
}