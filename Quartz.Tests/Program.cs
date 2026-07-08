List<(string Name, Action Run)> tests = [
    ("SemVer parses and orders channels", SemVerTests.TestSemVer),
    ("SemVer formats and parses channels", SemVerTests.TestSemVerFormatAndChannels),
    ("AtomicFile replaces without temp debris", AtomicFileTests.TestAtomicFile),
    ("Imported mod profile names are sanitized and uniquified", ProfileNamesTests.TestImportedModProfileNames),
    ("Localization keys stay in parity", LocalizationParityTests.TestLocalizationParity),
    ("KeyViewer CSS parses the DM Note contract", KeyViewerCssTests.TestKeyViewerCss),
    ("KeyViewer CSS parses the extended web effects", KeyViewerCssTests.TestKeyViewerCssExtended),
];

int failed = 0;
foreach((string name, Action run) in tests) {
    try {
        run();
        Console.WriteLine("PASS " + name);
    } catch(Exception e) {
        failed++;
        Console.Error.WriteLine("FAIL " + name + ": " + e.Message);
    }
}

return failed == 0 ? 0 : 1;
