using System.Text.Json;
using System.Text.Json.Serialization;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.Core.Diagrams;
using ILSight.Core.Metadata;
using ILSight.BookmarkArea;
using ILSight.AssemblyListArea;
using ILSight.DisplayOptionArea;
using ILSight.NuGet;
using ILSight.SettingsKeeping;

namespace ILSight.AppSettingArea;

[JsonSourceGenerationOptions(WriteIndented = true, Converters =
[
    typeof(EnumByName<ApiScope>), typeof(EnumByName<ApiVisibility>), typeof(EnumByName<BookmarkKind>),
    typeof(EnumByName<CodeAnchorKind>), typeof(EnumByName<CSharpVersion>), typeof(EnumByName<DecompilationLanguage>),
    typeof(EnumByName<Direction>), typeof(EnumByName<Locale>), typeof(EnumByName<MatchMode>),
    typeof(EnumByName<MemberSection>), typeof(EnumByName<SearchKinds>),
    typeof(EnumByName<TokenFormat>), typeof(EnumByName<TriState>),
])]
[JsonSerializable(typeof(SettingsFile))]
[JsonSerializable(typeof(BookmarkFileJson))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<JsonElement>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(DecompilationLanguage?))]
[JsonSerializable(typeof(ApiVisibility?))]
[JsonSerializable(typeof(SearchKinds?))]
[JsonSerializable(typeof(Locale?))]
[JsonSerializable(typeof(WindowBoundsJson))]
[JsonSerializable(typeof(DecompilerOptionsJson))]
[JsonSerializable(typeof(DisplayOptionsJson))]
[JsonSerializable(typeof(DockLayoutJson))]
[JsonSerializable(typeof(List<string?>))]
[JsonSerializable(typeof(List<float>))]
[JsonSerializable(typeof(List<CodeAnchorJson?>))]
[JsonSerializable(typeof(Dictionary<string, SavedFilterStateJson?>))]
[JsonSerializable(typeof(NuGetSearchJson))]
[JsonSerializable(typeof(AssemblyListJson))]
[JsonSerializable(typeof(DocumentJson))]
[JsonSerializable(typeof(BookmarkJson))]
internal sealed partial class AppSettingsJson : JsonSerializerContext;
