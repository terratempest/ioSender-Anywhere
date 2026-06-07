using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace CNC.Controls.Probing;

[Serializable]
public class ProbingProfile
{
    [XmlIgnore]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public double Offset { get; set; }
    public double XYClearance { get; set; }
    public double Depth { get; set; }
    public double ProbeDistance { get; set; }
    public double LatchDistance { get; set; }
    public double DistanceZ { get; set; }
    public double ProbeFeedRate { get; set; }
    public double LatchFeedRate { get; set; }
    public double RapidsFeedRate { get; set; }
    public double ProbeDiameter { get; set; }
    public double TouchPlateHeight { get; set; }
    public double FixtureHeight { get; set; }
    public double ProbeOffsetX { get; set; }
    public double ProbeOffsetY { get; set; }
    public bool TouchPlateIsXY { get; set; }
}

public sealed class ProbingProfiles
{
    int _id;

    public ObservableCollection<ProbingProfile> Profiles { get; private set; } = [];

    public int Add(string name, ProbingPanelViewModel data)
    {
        Profiles.Add(new ProbingProfile
        {
            Id = _id++,
            Name = name,
            RapidsFeedRate = data.RapidsFeedRate,
            ProbeFeedRate = data.ProbeFeedRate,
            LatchFeedRate = data.LatchFeedRate,
            ProbeDistance = data.ProbeDistance,
            LatchDistance = data.LatchDistance,
            ProbeDiameter = data.ProbeDiameter,
            Offset = data.Offset,
            ProbeOffsetX = data.ProbeOffsetX,
            ProbeOffsetY = data.ProbeOffsetY,
            XYClearance = data.XYClearance,
            Depth = data.Depth,
            TouchPlateHeight = data.TouchPlateHeight,
            TouchPlateIsXY = data.TouchPlateIsXY,
            FixtureHeight = data.FixtureHeight
        });

        return _id - 1;
    }

    public void Update(int id, string name, ProbingPanelViewModel data)
    {
        var profile = Profiles.FirstOrDefault(x => x.Id == id);
        if (profile == null)
            return;

        profile.Name = name;
        profile.RapidsFeedRate = data.RapidsFeedRate;
        profile.ProbeFeedRate = data.ProbeFeedRate;
        profile.LatchFeedRate = data.LatchFeedRate;
        profile.ProbeDistance = data.ProbeDistance;
        profile.LatchDistance = data.LatchDistance;
        profile.ProbeDiameter = data.ProbeDiameter;
        profile.Offset = data.Offset;
        profile.ProbeOffsetX = data.ProbeOffsetX;
        profile.ProbeOffsetY = data.ProbeOffsetY;
        profile.XYClearance = data.XYClearance;
        profile.Depth = data.Depth;
        profile.TouchPlateHeight = data.TouchPlateHeight;
        profile.TouchPlateIsXY = data.TouchPlateIsXY;
        profile.FixtureHeight = data.FixtureHeight;
    }

    public bool Delete(int id)
    {
        var profile = Profiles.FirstOrDefault(x => x.Id == id);
        if (profile == null || Profiles.Count <= 1)
            return false;

        return Profiles.Remove(profile);
    }

    public void Save()
    {
        var path = Core.Resources.ConfigPath + "ProbingProfiles.xml";
        var xs = new XmlSerializer(typeof(ObservableCollection<ProbingProfile>));
        try
        {
            using var fs = File.Create(path);
            xs.Serialize(fs, Profiles);
        }
        catch (Exception e)
        {
            CNC.Core.GrblUi.ShowError(e.Message);
        }
    }

    public void Load()
    {
        var path = Core.Resources.ConfigPath + "ProbingProfiles.xml";
        var xs = new XmlSerializer(typeof(ObservableCollection<ProbingProfile>));

        try
        {
            using var reader = new StreamReader(path);
            Profiles = (ObservableCollection<ProbingProfile>)xs.Deserialize(reader)!;
            foreach (var profile in Profiles)
                profile.Id = _id++;
        }
        catch
        {
            Profiles = [];
        }

        if (Profiles.Count == 0)
        {
            Profiles.Add(new ProbingProfile
            {
                Id = _id++,
                Name = "<Default>",
                RapidsFeedRate = 0d,
                ProbeFeedRate = 100d,
                LatchFeedRate = 25d,
                ProbeDistance = 10d,
                LatchDistance = .5d,
                ProbeDiameter = 2d,
                XYClearance = 5d,
                Offset = 5d,
                ProbeOffsetX = 0d,
                ProbeOffsetY = 0d,
                Depth = 3d,
                TouchPlateHeight = 1d,
                FixtureHeight = 1d
            });
        }
    }
}

[Serializable]
public sealed class ProbingProfileUsage
{
    public ProbingType ProbingType { get; set; }
    public string ProfileName { get; set; } = string.Empty;
}

public sealed class ProbingProfileUsageStore
{
    readonly List<ProbingProfileUsage> _profiles = [];
    bool _loaded;

    public string? Get(ProbingType probingType) =>
        _profiles.FirstOrDefault(x => x.ProbingType == probingType)?.ProfileName;

    public void Set(ProbingType probingType, ProbingProfile? profile)
    {
        if (probingType == ProbingType.None || profile == null)
            return;

        var usage = _profiles.FirstOrDefault(x => x.ProbingType == probingType);
        if (usage == null)
        {
            _profiles.Add(new ProbingProfileUsage
            {
                ProbingType = probingType,
                ProfileName = profile.Name
            });
            return;
        }

        usage.ProfileName = profile.Name;
    }

    public void Save()
    {
        try
        {
            var path = Core.Resources.ConfigPath + "ProbingLastUsedProfiles.xml";
            var xs = new XmlSerializer(typeof(List<ProbingProfileUsage>));
            using var fs = File.Create(path);
            xs.Serialize(fs, _profiles);
        }
        catch (Exception e)
        {
            CNC.Core.GrblUi.ShowError(e.Message);
        }
    }

    public void Load()
    {
        if (_loaded)
            return;

        _loaded = true;

        try
        {
            var path = Core.Resources.ConfigPath + "ProbingLastUsedProfiles.xml";
            var xs = new XmlSerializer(typeof(List<ProbingProfileUsage>));
            using var reader = new StreamReader(path);
            _profiles.Clear();
            _profiles.AddRange((List<ProbingProfileUsage>)xs.Deserialize(reader)!);
        }
        catch
        {
            _profiles.Clear();
        }
    }
}
