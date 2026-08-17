using System;
namespace Quartz.Features.AprilFools;
public enum QuizDifficulty { Grade1to3, Grade4to5, Grade6to7, Grade8to9, Grade10to11, Grade12, Impossible }
public enum QuizSubject { Math, Language, Science, Social, Arts, Void }
public sealed class QuizQuestion {
    public string Prompt { get; }
    public string[] Options { get; }
    public int CorrectIndex { get; }
    public QuizSubject Subject { get; }
    public QuizQuestion(QuizSubject subject, string prompt, string[] options, int correctIndex) {
        Subject = subject;
        Prompt = prompt;
        Options = options;
        CorrectIndex = correctIndex;
    }
}
public static class QuizBank {
    private static readonly Random Rng = new();
    public static QuizDifficulty RandomNormal() => (QuizDifficulty)Rng.Next(0, 6);
    public static QuizQuestion Pick(QuizDifficulty difficulty) {
        QuizQuestion[] pool = PoolFor(difficulty);
        return pool[Rng.Next(pool.Length)];
    }
    public static QuizQuestion PickOther(QuizDifficulty difficulty, QuizQuestion previous) {
        QuizQuestion[] pool = PoolFor(difficulty);
        if(pool.Length < 2) return pool[0];
        QuizQuestion next = pool[Rng.Next(pool.Length)];
        while(ReferenceEquals(next, previous)) next = pool[Rng.Next(pool.Length)];
        return next;
    }
    public static QuizQuestion[] PoolFor(QuizDifficulty difficulty) => difficulty switch {
        QuizDifficulty.Grade1to3 => Grade1to3,
        QuizDifficulty.Grade4to5 => Grade4to5,
        QuizDifficulty.Grade6to7 => Grade6to7,
        QuizDifficulty.Grade8to9 => Grade8to9,
        QuizDifficulty.Grade10to11 => Grade10to11,
        QuizDifficulty.Grade12 => Grade12,
        _ => Impossible,
    };
    private static QuizQuestion M(string prompt, int correct, params string[] options) =>
        new(QuizSubject.Math, prompt, options, correct);
    private static QuizQuestion L(string prompt, int correct, params string[] options) =>
        new(QuizSubject.Language, prompt, options, correct);
    private static QuizQuestion S(string prompt, int correct, params string[] options) =>
        new(QuizSubject.Science, prompt, options, correct);
    private static QuizQuestion H(string prompt, int correct, params string[] options) =>
        new(QuizSubject.Social, prompt, options, correct);
    private static QuizQuestion A(string prompt, int correct, params string[] options) =>
        new(QuizSubject.Arts, prompt, options, correct);
    private static QuizQuestion V(string prompt, params string[] options) =>
        new(QuizSubject.Void, prompt, options, -1);
    private static readonly QuizQuestion[] Grade1to3 = {
        M("7 + 8 = ?", 2, "13", "14", "15", "16"),
        M("12 − 5 = ?", 1, "6", "7", "8", "9"),
        M("6 × 4 = ?", 2, "18", "22", "24", "28"),
        M("36 ÷ 6 = ?", 0, "6", "7", "5", "8"),
        M("9 + 16 = ?", 3, "23", "24", "26", "25"),
        M("13 − 9 = ?", 1, "3", "4", "5", "6"),
        M("7 × 7 = ?", 2, "42", "47", "49", "56"),
        M("45 ÷ 9 = ?", 0, "5", "6", "4", "9"),
        M("18 + 27 = ?", 3, "43", "44", "46", "45"),
        M("52 − 34 = ?", 1, "16", "18", "22", "28"),
        M("8 × 6 = ?", 2, "42", "46", "48", "54"),
        M("100 ÷ 4 = ?", 0, "25", "24", "20", "40"),
        L("Which word is spelled correctly?", 1, "bananna", "banana", "banan", "bannana"),
        L("A baby dog is called a…", 2, "kitten", "cub", "puppy", "calf"),
        L("Which letter is a vowel?", 1, "B", "E", "K", "T"),
        L("The plural of 'mouse' is…", 1, "mouses", "mice", "mouse", "mices"),
        S("How many legs does an insect have?", 2, "4", "8", "6", "10"),
        S("Water turns into ice when it…", 2, "boils", "melts", "freezes", "rains"),
        S("Which animal lays eggs?", 1, "cow", "hen", "cat", "horse"),
        S("The Sun rises in the…", 2, "north", "west", "east", "south"),
        H("How many continents are on Earth?", 2, "5", "6", "7", "8"),
        H("The largest ocean is the…", 1, "Atlantic", "Pacific", "Indian", "Arctic"),
        A("Blue and yellow paint make…", 2, "purple", "orange", "green", "brown"),
        A("How many colors are in a rainbow?", 2, "5", "6", "7", "8"),
    };
    private static readonly QuizQuestion[] Grade4to5 = {
        M("17 × 6 = ?", 1, "96", "102", "108", "112"),
        M("204 ÷ 17 = ?", 2, "10", "11", "12", "14"),
        M("3/4 + 1/8 = ?", 0, "7/8", "4/12", "5/8", "1"),
        M("25% of 84 = ?", 3, "18", "24", "28", "21"),
        M("13² = ?", 1, "159", "169", "179", "196"),
        M("7 × 8 − 14 = ?", 2, "38", "40", "42", "44"),
        M("5/6 of 42 = ?", 0, "35", "36", "30", "34"),
        M("0.2 × 0.5 = ?", 3, "1", "0.7", "0.01", "0.1"),
        M("19 + 26 + 37 = ?", 1, "78", "82", "84", "88"),
        M("2/3 − 1/4 = ?", 2, "1/2", "1/12", "5/12", "7/12"),
        M("15% of 260 = ?", 0, "39", "36", "42", "45"),
        M("What is 3.5 × 8?", 1, "24", "28", "30", "32"),
        L("A synonym of 'happy' is…", 0, "glad", "angry", "tired", "slow"),
        L("The opposite of 'ancient' is…", 1, "old", "modern", "broken", "giant"),
        L("The past tense of 'run' is…", 1, "runned", "ran", "runed", "running"),
        L("A group of wolves is called a…", 2, "herd", "flock", "pack", "school"),
        S("H₂O is better known as…", 2, "salt", "oxygen", "water", "hydrogen"),
        S("Which planet is closest to the Sun?", 3, "Venus", "Earth", "Mars", "Mercury"),
        S("Plants make their own food using…", 1, "digestion", "photosynthesis", "respiration", "fermentation"),
        S("Sound travels fastest through…", 3, "air", "water", "empty space", "steel"),
        H("The Great Wall is in…", 1, "Japan", "China", "India", "Egypt"),
        H("Which river flows through Egypt?", 1, "Amazon", "Nile", "Danube", "Ganges"),
        H("The capital of Japan is…", 2, "Kyoto", "Osaka", "Tokyo", "Seoul"),
        A("A music staff has how many lines?", 1, "4", "5", "6", "7"),
    };
    private static readonly QuizQuestion[] Grade6to7 = {
        M("3x + 7 = 22. x = ?", 2, "3", "4", "5", "6"),
        M("x/4 + 3 = 10. x = ?", 2, "24", "26", "28", "32"),
        M("5x − 9 = 3x + 15. x = ?", 1, "8", "12", "10", "14"),
        M("gcd(84, 126) = ?", 3, "14", "21", "28", "42"),
        M("lcm(6, 8) = ?", 0, "24", "48", "12", "16"),
        M("−7 + 12 = ?", 1, "−5", "5", "19", "−19"),
        M("(−3) × (−6) = ?", 2, "−18", "−9", "18", "9"),
        M("40% of 85 = ?", 0, "34", "32", "36", "38"),
        M("Split 40 in the ratio 3 : 5. The larger part is…", 3, "15", "24", "30", "25"),
        M("2 + 3 × 4 − 5 = ?", 1, "15", "9", "7", "0"),
        M("|−9| − |4| = ?", 2, "−5", "13", "5", "−13"),
        M("The average of 12, 15, and 21 is…", 0, "16", "15", "17", "18"),
        L("Which word is a preposition?", 0, "under", "jump", "blue", "slowly"),
        L("The opposite of 'expand' is…", 1, "explode", "contract", "extend", "inflate"),
        L("'Brunch' is a blend of which two words?", 1, "bread + lunch", "breakfast + lunch", "brown + crunch", "break + munch"),
        L("Which word is a noun?", 2, "quickly", "bright", "freedom", "run"),
        S("The chemical symbol for gold is…", 2, "Go", "Gd", "Au", "Ag"),
        S("Plants absorb which gas?", 1, "oxygen", "carbon dioxide", "nitrogen", "helium"),
        S("The 'powerhouse of the cell' is the…", 2, "nucleus", "ribosome", "mitochondrion", "membrane"),
        S("The layer between Earth's crust and core is the…", 0, "mantle", "ozone", "magma sea", "plate"),
        H("The Colosseum was built by the…", 1, "Greeks", "Romans", "Egyptians", "Vikings"),
        H("The Nile empties into which sea?", 2, "Red Sea", "Black Sea", "Mediterranean", "Caspian"),
        H("Which country currently has the largest population?", 1, "China", "India", "USA", "Indonesia"),
        A("Who painted the Mona Lisa?", 3, "Michelangelo", "Raphael", "Rembrandt", "Leonardo da Vinci"),
    };
    private static readonly QuizQuestion[] Grade8to9 = {
        M("2⁸ = ?", 3, "128", "512", "254", "256"),
        M("√196 = ?", 0, "14", "13", "16", "12"),
        M("A right triangle has legs 9 and 12. Hypotenuse = ?", 0, "15", "14", "16", "13"),
        M("x² = 121 and x > 0. x = ?", 1, "10", "11", "12", "13"),
        M("2x² = 50 and x > 0. x = ?", 1, "4", "5", "6", "10"),
        M("1 + 2 + 3 + … + 20 = ?", 0, "210", "190", "220", "200"),
        M("x² − 9 factors as…", 0, "(x−3)(x+3)", "(x−3)²", "(x−9)(x+1)", "x(x−9)"),
        M("Slope of the line through (1, 2) and (4, 11) = ?", 0, "3", "2", "4", "9/5"),
        M("3⁴ − 4³ = ?", 3, "13", "15", "19", "17"),
        M("17 is the n-th prime. n = ?", 3, "5", "6", "8", "7"),
        M("|−7| + |3 − 9| = ?", 3, "1", "10", "15", "13"),
        M("7! ÷ 5! = ?", 1, "35", "42", "49", "56"),
        L("'The wind whispered' is an example of…", 2, "simile", "metaphor", "personification", "hyperbole"),
        L("The plural of 'crisis' is…", 1, "crisises", "crises", "crisi", "crisen"),
        L("'Ubiquitous' means…", 1, "rare", "found everywhere", "invisible", "ancient"),
        L("Which phrase is an oxymoron?", 0, "deafening silence", "loud noise", "bright light", "cold ice"),
        S("In F = ma, the a stands for…", 2, "area", "altitude", "acceleration", "amplitude"),
        S("The pH of pure water is…", 2, "0", "5", "7", "14"),
        S("The most abundant gas in Earth's atmosphere is…", 2, "oxygen", "carbon dioxide", "nitrogen", "argon"),
        S("DNA is short for…", 0, "deoxyribonucleic acid", "dinucleic acid", "dual nitrogen array", "deoxyribose neutral acid"),
        H("World War I began in…", 1, "1905", "1914", "1918", "1939"),
        H("Natural selection was proposed by…", 2, "Newton", "Mendel", "Darwin", "Pasteur"),
        H("The capital of Australia is…", 2, "Sydney", "Melbourne", "Canberra", "Perth"),
        H("Machu Picchu was built by the…", 2, "Maya", "Aztec", "Inca", "Olmec"),
    };
    private static readonly QuizQuestion[] Grade10to11 = {
        M("log₂ 64 = ?", 2, "5", "8", "6", "7"),
        M("sin 30° = ?", 1, "√3/2", "1/2", "√2/2", "1"),
        M("cos 60° = ?", 2, "√3/2", "√2/2", "1/2", "0"),
        M("tan 45° = ?", 0, "1", "0", "√2", "√3"),
        M("The roots of x² − 5x + 6 are…", 0, "2 and 3", "−2 and −3", "1 and 6", "−1 and 6"),
        M("i² = ?", 1, "1", "−1", "i", "−i"),
        M("C(5, 2) = ?", 1, "20", "10", "15", "25"),
        M("e⁰ + ln 1 = ?", 2, "0", "e", "1", "2"),
        M("det [[2, 1], [3, 4]] = ?", 3, "11", "8", "2", "5"),
        M("The argument of z = 1 + i is…", 3, "30°", "60°", "90°", "45°"),
        M("Trace of [[3, 1], [0, 2]] = ?", 3, "6", "3", "2", "5"),
        M("The 7th Fibonacci number (1, 1, 2, …) = ?", 2, "8", "11", "13", "21"),
        L("Who wrote 'Hamlet'?", 1, "Marlowe", "Shakespeare", "Milton", "Chaucer"),
        L("'Schadenfreude' means…", 1, "fear of heights", "joy at another's misfortune", "love of learning", "dread of Mondays"),
        L("A 14-line poem is a…", 2, "haiku", "limerick", "sonnet", "ballad"),
        L("'They had gone' is in which tense?", 0, "past perfect", "present perfect", "simple past", "future perfect"),
        S("The speed of light is about…", 1, "3×10⁶ m/s", "3×10⁸ m/s", "3×10¹⁰ m/s", "340 m/s"),
        S("The atomic number of carbon is…", 1, "4", "6", "8", "12"),
        S("Newton's third law: every action has an…", 1, "equal parallel action", "equal and opposite reaction", "opposite smaller reaction", "unrelated reaction"),
        S("Which particle carries no electric charge?", 2, "proton", "electron", "neutron", "ion"),
        H("The French Revolution began in…", 1, "1776", "1789", "1804", "1848"),
        H("'The Communist Manifesto' was written by…", 1, "Lenin and Trotsky", "Marx and Engels", "Stalin", "Hegel"),
        H("Which strait separates Europe from Africa?", 2, "Bosporus", "Hormuz", "Gibraltar", "Malacca"),
        H("If supply rises while demand stays flat, price tends to…", 1, "rise", "fall", "stay exactly fixed", "double"),
    };
    private static readonly QuizQuestion[] Grade12 = {
        M("d/dx x³ at x = 2 equals…", 2, "8", "6", "12", "24"),
        M("∫₀¹ 2x dx = ?", 1, "2", "1", "1/2", "0"),
        M("lim(x→0) sin x / x = ?", 0, "1", "0", "∞", "undefined"),
        M("Σ (1/2)ⁿ for n = 0 to ∞ equals…", 3, "1", "3/2", "e", "2"),
        M("d/dx ln x at x = 4 equals…", 1, "4", "1/4", "ln 4", "1"),
        M("∫₀^π sin x dx = ?", 0, "2", "0", "π", "1"),
        M("lim(n→∞) (1 + 1/n)ⁿ = ?", 1, "1", "e", "π", "∞"),
        M("d/dx e²ˣ at x = 0 equals…", 2, "0", "1", "2", "e²"),
        M("Σ k² for k = 1 to 10 equals…", 0, "385", "355", "380", "405"),
        M("d/dx sin x at x = 0 equals…", 1, "0", "1", "−1", "cos 1"),
        M("∫₁^e (1/x) dx = ?", 3, "e", "0", "ln e²", "1"),
        M("lim(x→∞) (3x² + 1)/(x² − 5) = ?", 2, "0", "∞", "3", "−3/5"),
        L("'Cogito, ergo sum' is from…", 2, "Plato", "Kant", "Descartes", "Nietzsche"),
        L("Attacking the speaker instead of the argument is…", 1, "straw man", "ad hominem", "red herring", "slippery slope"),
        L("Who wrote '1984'?", 1, "Huxley", "Orwell", "Kafka", "Bradbury"),
        L("'Existence precedes essence' is associated with…", 0, "Sartre", "Aquinas", "Descartes", "Hume"),
        S("In E = mc², m stands for…", 1, "momentum", "mass", "magnitude", "matter density"),
        S("In a closed system, entropy tends to…", 2, "decrease", "stay constant", "increase", "oscillate"),
        S("Heisenberg's uncertainty principle pairs position with…", 2, "energy", "spin", "momentum", "charge"),
        S("After two half-lives, how much of a sample remains?", 1, "1/2", "1/4", "1/8", "none"),
        H("The United Nations was founded in…", 1, "1919", "1945", "1950", "1961"),
        H("'The Wealth of Nations' was written by…", 2, "Ricardo", "Keynes", "Adam Smith", "Malthus"),
        H("The Peace of Westphalia (1648) is credited with establishing…", 2, "feudalism", "papal supremacy", "state sovereignty", "free trade"),
        H("GDP measures a country's…", 2, "gold reserves", "national debt", "total production of goods and services", "tax revenue"),
    };
    private static readonly QuizQuestion[] Impossible = {
        V("Prove the Riemann Hypothesis. Show your work.", "Yes", "No", "ζ(s)", "Trivially"),
        V("Solve for x: x has left the equation.", "x", "Come back", "∅", "404"),
        V("What is the last digit of π?", "7", "0", "9", "π"),
        V("This statement is false. True or false?", "True", "False", "Both", "Blue"),
        V("Count every real number between 0 and 1. Round to the nearest integer.", "1", "ℵ₀", "2^ℵ₀", "12"),
        V("Divide by zero. Report the quotient.", "0", "∞", "NaN", "The void"),
        V("Find the largest prime number.", "That one", "2", "It's even", "No"),
        V("What number am I thinking of?", "7", "7", "7", "Not 7"),
        V("BB(748) = ?", "Big", "Very big", "Unknowable", "748"),
        V("How many corners does a circle gain per lap?", "0", "1", "∞", "Depends"),
        V("∫∫∫∫∫∫ dx dy dz dw dv du over a non-measurable set = ?", "0", "1", "Undefined", "Six"),
        V("If 7 trains leave the station at the speed of light, when do they arrive?", "Now", "Never", "Yes", "3:40 PM"),
        V("What color is Tuesday?", "Blue", "Loud", "7", "Yes"),
        V("Translate this question into a language nobody speaks.", "Done", "∅", "Mhm", "[silence]"),
        V("Name every person in history, alphabetically.", "Aaron…", "Everyone", "No", "Zzyzx"),
        V("Which came first: the chicken, the egg, or this question?", "Chicken", "Egg", "This question", "Yes"),
        V("Summarize the entire internet in one word.", "Cats", "No", "Loading…", "404"),
        V("Spell 'onomatopoeia' backwards while proving you exist.", "aieopotamono", "I think not", "Therefore", "Boom"),
    };
}
