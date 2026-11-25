using System;

namespace MiHomeUnitTests.Robustness;

internal static class SeededMutator
{
    public const int Seed = 0x4D49484F;

    public static int Iterations
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("MIHOME_FUZZ_ITERATIONS");
            if (value is null) return 2_000;
            if (int.TryParse(value, out var iterations) && iterations > 0) return iterations;
            throw new InvalidOperationException("MIHOME_FUZZ_ITERATIONS must be a positive integer.");
        }
    }

    public static string Mutate(string input, Random random)
    {
        var operation = random.Next(4);
        if (input.Length == 0) operation = 2;
        var start = random.Next(input.Length + 1);
        var length = random.Next(input.Length - start + 1);
        return operation switch
        {
            0 when length > 0 => input.Remove(start, length),
            1 when input.Length > 0 => input[..random.Next(input.Length)] + RandomCharacter(random) + input[(random.Next(input.Length)..)],
            2 => input.Insert(start, RandomFragment(random)),
            _ when length > 0 => input.Insert(start, input.Substring(start, length)),
            _ => input + RandomFragment(random),
        };
    }

    public static string RandomText(Random random, int maxLength = 100)
    {
        var length = random.Next(maxLength + 1);
        var text = new char[length];
        for (var index = 0; index < text.Length; index++) text[index] = RandomCharacter(random);
        return new string(text);
    }

    private static string RandomFragment(Random random)
    {
        var count = random.Next(1, 5);
        var text = new char[count];
        for (var index = 0; index < text.Length; index++) text[index] = RandomCharacter(random);
        return new string(text);
    }

    private static char RandomCharacter(Random random) =>
        (char)random.Next(0x20, 0x7f);
}
