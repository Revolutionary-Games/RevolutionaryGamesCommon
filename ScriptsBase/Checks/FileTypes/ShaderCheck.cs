namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;

public class ShaderCheck : LineByLineFileChecker
{
    private bool checkingLength = true;

    public ShaderCheck() : base(".shader")
    {
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        LineCharacterHelpers.HandleLineLengthCheckControlComments(line, ref checkingLength);

        var tabError = LineCharacterHelpers.CheckLineForTab(line, lineNumber);
        if (tabError != null)
            yield return tabError;

        var lengthError = LineCharacterHelpers.CheckLineForBeingTooLong(line, lineNumber, checkingLength);
        if (lengthError != null)
            yield return lengthError;
    }
}
