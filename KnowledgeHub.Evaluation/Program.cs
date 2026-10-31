using KnowledgeHub.Evaluation;

// KH007 is deliberately offline. It never contacts the API, Ollama or PostgreSQL.
// Reference observations are oracle fixtures, not measured model performance.
if (args.Length is not 5 and not 7 || args[0] is not ("--self-test" or "--evaluate") ||
    args[1] != "--baseline" || args[3] != "--report" ||
    (args[0] == "--self-test" && args.Length != 5) ||
    (args[0] == "--evaluate" && (args.Length != 7 || args[5] != "--predictions")))
{
    Console.Error.WriteLine("Usage: --self-test --baseline DIR --report DIR | " +
        "--evaluate --baseline DIR --report DIR --predictions FILE");
    return 2;
}

try
{
    var baseline = ReferenceEvaluator.Load(args[2]);
    PredictionSet predictions;
    string label;
    if (args[0] == "--self-test")
    {
        ReferenceEvaluator.SelfTest(baseline);
        predictions = baseline.Oracle;
        label = "synthetic-oracle (not a measured RAG run)";
    }
    else
    {
        predictions = ReferenceEvaluator.LoadPredictions(args[6]);
        label = "supplied-prediction-file";
    }
    var report = ReferenceEvaluator.Evaluate(baseline, predictions);
    ReferenceEvaluator.Save(report, args[4], label);
    Console.WriteLine("KH007_REFERENCE_RESULT=pass");
    Console.WriteLine("KH007_REFERENCE_CASES=" + report.CaseCount);
    Console.WriteLine("KH007_REFERENCE_ANSWERABLE=" + report.AnswerableCases);
    Console.WriteLine("KH007_REFERENCE_ABSTENTIONS=" + report.AbstentionCases);
    return 0;
}
catch (Exception ex) when (ex is InvalidOperationException or IOException or
    UnauthorizedAccessException or System.Text.Json.JsonException)
{
    // No untrusted raw JSON, path, prompt, model response or exception detail.
    Console.Error.WriteLine("KH007_REFERENCE_RESULT=failed");
    Console.Error.WriteLine("KH007_REFERENCE_ERROR=" + ex.GetType().Name);
    return 30;
}
