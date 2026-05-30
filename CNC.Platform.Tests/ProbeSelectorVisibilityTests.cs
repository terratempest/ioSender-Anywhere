using CNC.Core;
using CNC.Controls.Probing;

namespace CNC.Platform.Tests;

public sealed class ProbeSelectorVisibilityTests
{
    [Fact]
    public void GrblViewModel_MultiProbe_follows_probe_collection_changes()
    {
        var snapshot = GrblInfo.Probes.ToList();
        try
        {
            GrblInfo.Probes.Clear();
            GrblInfo.Probes.Add(new Probe(0, "Primary"));
            var vm = new GrblViewModel();
            var changed = new List<string?>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Assert.False(vm.MultiProbe);

            GrblInfo.Probes.Add(new Probe(1, "Toolsetter"));

            Assert.True(vm.MultiProbe);
            Assert.Contains(nameof(GrblViewModel.MultiProbe), changed);
        }
        finally
        {
            GrblInfo.Probes.Clear();
            foreach (var probe in snapshot)
                GrblInfo.Probes.Add(probe);
        }
    }

    [Fact]
    public void ProbingViewModel_MultiProbe_forwards_grbl_probe_collection_changes()
    {
        var snapshot = GrblInfo.Probes.ToList();
        try
        {
            GrblInfo.Probes.Clear();
            GrblInfo.Probes.Add(new Probe(0, "Primary"));
            var grbl = new GrblViewModel();
            var probing = new ProbingViewModel();
            var changed = new List<string?>();
            probing.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            probing.Attach(grbl);

            Assert.False(probing.MultiProbe);

            GrblInfo.Probes.Add(new Probe(1, "Toolsetter"));

            Assert.True(probing.MultiProbe);
            Assert.Contains(nameof(ProbingViewModel.MultiProbe), changed);
        }
        finally
        {
            GrblInfo.Probes.Clear();
            foreach (var probe in snapshot)
                GrblInfo.Probes.Add(probe);
        }
    }
}
