using System.Collections.Generic;
using System.IO;
using static Xamarin.Provisioning.ProvisioningScript;

static readonly bool CI = !string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("TF_BUILD"));

if (CI) {
	foreach (var dir in Directory.GetDirectories ("/Applications", "Xcode*"))
		Console.WriteLine ($"\tXcode Found: {dir}");
}

// We expect different naming than the provided one.
EnsureXcodeNaming ();


void EnsureXcodeNaming ()
{
    var xcodes = new Dictionary<string, string> {
        {"/Applications/Xcode_10.2.app", "/Applications/Xcode102.app"},
        {"/Applications/Xcode_10.1.app", "/Applications/Xcode101.app"},
        {"/Applications/Xcode_10.app", "/Applications/Xcode10.app"},
        {"/Applications/Xcode_9.4.1.app", "/Applications/Xcode941.app"},
        {"/Applications/Xcode_9.4.app", "/Applications/Xcode94.app"},
        {"/Applications/Xcode_9.3.1.app", "/Applications/Xcode931.app"},
        {"/Applications/Xcode_9.3.app", "/Applications/Xcode93.app"},
        {"/Applications/Xcode_9.2.app", "/Applications/Xcode92.app"},
        {"/Applications/Xcode_9.1.app", "/Applications/Xcode91.app"},
        {"/Applications/Xcode_9.0.1.app", "/Applications/Xcode901.app"},
        {"/Applications/Xcode_9.app", "/Applications/Xcode9.app"},
        {"/Applications/Xcode_8.3.3.app", "/Applications/Xcode833.app"},
    };

    foreach (var xcode in xcodes) {
        if (Directory.Exists (xcode.Key)) {
            if (!Directory.Exists (xcode.Value))
                ln (xcode.Key, xcode.Value);
        }
    }
}

void ln (string source, string destination)
{
	Console.WriteLine ($"ln -sf {source} {destination}");
	if (!Config.DryRun)
		Exec ("/bin/ln", "-sf", source, destination);
}
