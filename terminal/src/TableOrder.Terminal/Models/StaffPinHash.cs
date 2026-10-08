namespace TableOrder.Terminal.Models;

// スタッフの PIN のハッシュ (PBKDF2-HMAC-SHA256 の回数と、Base64 の塩とハッシュ)
public sealed record StaffPinHash(int Iterations, string Salt, string Hash);
