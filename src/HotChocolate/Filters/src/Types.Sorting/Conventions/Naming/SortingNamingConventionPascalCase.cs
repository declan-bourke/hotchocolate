using System;

namespace HotChocolate.Types.Sorting;

[Obsolete("Use HotChocolate.Data.")]
public class SortingNamingConventionPascalCase : SortingNamingConventionBase
{
    public override string ArgumentName => "OrderBy";
}
