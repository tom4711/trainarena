
# Task final-fix report

Branch: cursor/question-types-design-b12d

## Fixed
1. `QuizRules.CollectOptions` (trim, drop trailing empties, keep interior gaps); used by editor AddQuestion and QuizImport (BuildFilledOptions removed). A,B,"",D now yields "Antwortoptionen dürfen keine Lücken haben."
2. TrueFalse requires exact "Wahr"/"Falsch" ("Wahr/Falsch erfordert die Optionen Wahr und Falsch."); editor uses QuizRules.TrueLabel/FalseLabel.
3. Tests: EnsureSchema_UpgradesLegacyQuestionsTable; FullRoundTests.TrueFalseAndTwoOptionMcQuiz_ScoresCorrectAnswersEndToEnd; QuizRulesTests for CollectOptions and TrueFalse labels.
4. GameSession FiftyFifty: removed unreachable maskCount==0 branch.

## Verification
Command: `dotnet test TrainArena.sln`
Output: `Passed!  - Failed: 0, Passed: 116, Skipped: 0, Total: 116, Duration: 14 s - TrainArena.Tests.dll (net10.0)`
