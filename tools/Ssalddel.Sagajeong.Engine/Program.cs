using System.Text;
using Ssalddel.Sagajeong.Core;

// Windows 호환 호스트도 모바일과 같은 코어를 호출한다. 업무 규칙은 이 실행 파일에 두지 않는다.
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
var samples = Path.Combine(AppContext.BaseDirectory, "Samples");
var core = new SagajeongSimulationCore(
    File.ReadAllText(Path.Combine(samples, "delivery-sample.json")),
    File.ReadAllText(Path.Combine(samples, "road-graph.json")));
while (Console.ReadLine() is { } request)
    Console.WriteLine(core.Execute(request));
