namespace TableOrder.Domain.Enums;

// 品を出す時機 (AfterMeal は食後にお願いされるまで止めておく)
public enum OrderTiming
{
    Now,
    AfterMeal
}
