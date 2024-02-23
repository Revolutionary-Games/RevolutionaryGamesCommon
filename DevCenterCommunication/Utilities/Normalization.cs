namespace DevCenterCommunication.Utilities;

using System;
using System.Text;

public static class Normalization
{
    public static string NormalizeUserName(string username)
    {
        // TODO: Maybe some length limiting
        var builder = new StringBuilder(username.Length);

        bool addSpace = false;
        bool addDash = false;
        bool addedOtherCharacter = false;

        // An extended trim that also converts spaces to non-spaces and removes non-printing characters and special
        // characters
        foreach (var letter in username)
        {
            if (letter <= ' ')
            {
                if (addedOtherCharacter)
                {
                    addSpace = true;
                }
            }
            else if (IsUserNameAllowedCharacter(letter))
            {
                if (addSpace)
                {
                    builder.Append('_');
                    addSpace = false;
                }
                else if (addDash)
                {
                    builder.Append('-');
                    addDash = false;
                }

                addedOtherCharacter = true;
                builder.Append(letter);
            }
            else
            {
                // Non-alphanumeric range character, skip
                addDash = true;
            }
        }

        if (addDash)
            builder.Append('-');

        while (builder.Length < CommunicationConstants.MIN_USERNAME_LENGTH)
        {
            builder.Append(builder[^1]);
        }

        return builder.ToString(0, Math.Min(builder.Length, CommunicationConstants.MAX_USERNAME_LENGTH));
    }

    public static string NormalizeEmail(string email)
    {
        var split = email.Split('@');

        if (split.Length < 2)
            throw new ArgumentException("Email must contain '@'");

        var stringBuilder = new StringBuilder(email.Length);

        // TODO: email comment removal?

        // Basic email normalization. Removes dots and everything after a + until the domain, and lower cases
        // everything.
        for (int i = 0; i < split.Length - 1; ++i)
        {
            var current = split[i].ToLowerInvariant();

            bool stop = false;

            foreach (var letter in current)
            {
                if (letter == '+')
                {
                    // Process only up to the first plus
                    stop = true;
                    break;
                }

                if (letter != '.')
                {
                    stringBuilder.Append(letter);
                }
            }

            if (stop)
                break;
        }

        stringBuilder.Append('@');
        stringBuilder.Append(split[^1].ToLowerInvariant());

        return stringBuilder.ToString();
    }

    public static bool IsUserNameAllowedCharacter(char letter)
    {
        // In alphanumeric range with dashes and dots
        if (letter is >= '-' and <= '9' and not '/')
            return true;

        if (letter is >= 'A' and <= 'Z')
            return true;

        if (letter is >= 'a' and <= 'z')
            return true;

        if (letter == '_')
            return true;

        return false;
    }
}
