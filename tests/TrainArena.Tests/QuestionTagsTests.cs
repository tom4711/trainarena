using TrainArena.Data;

namespace TrainArena.Tests;

public class QuestionTagsTests
{
    [Fact]
    public void NormalizeStorage_TrimsLowerAndDedupes()
    {
        var stored = QuestionTags.NormalizeStorage([" SQL ", "join", "sql", "Join"]);
        Assert.Equal("join,sql", stored);
    }

    [Fact]
    public void Parse_SplitsCommaAndSemicolon()
    {
        var tags = QuestionTags.Parse("Netzwerk; SQL, sql");
        Assert.Equal(["netzwerk", "sql"], tags);
    }

    [Fact]
    public void MatchesAny_OrSemantics()
    {
        Assert.True(QuestionTags.MatchesAny("sql,join", ["netzwerk", "JOIN"]));
        Assert.False(QuestionTags.MatchesAny("sql", ["netzwerk"]));
        Assert.True(QuestionTags.MatchesAny("sql", []));
        Assert.False(QuestionTags.MatchesAny(null, ["sql"]));
    }
}
