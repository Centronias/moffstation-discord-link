using MoffDiscordLink.Pagination;

namespace MoffDiscordLink.Pages.Tables;

public sealed record SortTabHeaderModel(ISortState State, string Column, string Text, TextAlignClass Align = TextAlignClass.Left);

public enum TextAlignClass
{
    Left,
    Right,
    Center
}
