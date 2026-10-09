namespace TableOrder.ReceptionApp.Modules.Guide;

// 案内の画面に渡す受付の結果。テーブルがなければ満席 (人数の入る空席がなかった)
public sealed record GuideResult(string? TableName, int Adults, int Children);
