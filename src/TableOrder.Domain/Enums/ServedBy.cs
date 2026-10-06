namespace TableOrder.Domain.Enums;

// 品を席まで運ぶのは誰か (Guest はお客様が自分でとる品。ドリンクバーやスープバーなど)
public enum ServedBy
{
    Staff,
    Guest
}
