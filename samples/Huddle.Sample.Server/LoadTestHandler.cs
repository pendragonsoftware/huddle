using System.Diagnostics;

namespace Huddle.Sample.Server;

public record LoadTestResults(int Recieved, int Expected, TimeSpan Duration, double Rps);

public static class LoadTestHandler
{
    private static int loadTestCount = 0;
    private static int loadTestExpected = 0;
    private static Stopwatch _loadTestStopWatch = new Stopwatch();

    public static void StartLoadTest(MessageBus messageBus, string amountExpected)
    {
        loadTestExpected = int.Parse(amountExpected);
        loadTestCount = 0;
        messageBus.PostApiMessage($"Starting load test. Expecting: {loadTestExpected}");
        _loadTestStopWatch.Start();
    }

    public static void ContinueLoadTest()
    {
        loadTestCount++;
    }

    public static LoadTestResults EndLoadTest(MessageBus messageBus)
    {
        _loadTestStopWatch.Stop();
        var totalSeconds = _loadTestStopWatch.Elapsed.TotalSeconds;
        var rps = loadTestCount / totalSeconds;

        messageBus.PostApiMessage($"Load test finished. Recieved {loadTestCount}/{loadTestExpected}. Took {_loadTestStopWatch.Elapsed} at {rps}rps");

        var results = new LoadTestResults(loadTestCount, loadTestExpected, _loadTestStopWatch.Elapsed, rps);

        _loadTestStopWatch.Reset();

        return results;
    }
}
