namespace AWL.Core.Categories;

/// <summary>Represents a category for grouping sessions.</summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The category name.</param>
/// <param name="SortOrder">The category's position in the list.</param>
public sealed record Category(Guid Id, string Name, int SortOrder = 0);
