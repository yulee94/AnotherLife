"""Compile the real engine-free catalog assembly and run focused NUnit assertions.

Requires an installed Unity toolchain and its compatible NUnit framework, not an
Editor process. Evidence is not Unity integration, multiplayer or device proof.
Sabotage compiles separate copied sources; tracked production files are never changed.
"""
import argparse
import pathlib
import shutil
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-data', type=pathlib.Path, required=True)
    parser.add_argument('--nunit', type=pathlib.Path, required=True)
    parser.add_argument('--sabotage', action='store_true')
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[2]
    output = root / 'archive/stronghold-verification'
    directory = root / 'unity/Assets/AL/Scripts/Data/Catalogs'
    production = sorted((directory / 'Strongholds').glob('*.cs'))
    production += [directory / name for name in ('StrictJson.cs', 'GameDataCatalogModels.cs', 'GameDataCatalogSources.cs')]
    tests = root / 'unity/Assets/AL/Tests/EditMode/Strongholds/StrongholdPlannerTests.cs'
    framework = args.nunit.resolve(strict=True)
    toolchain = args.unity_data.resolve(strict=True)
    dotnet = toolchain / 'NetCoreRuntime/dotnet.exe'
    compiler = toolchain / 'DotNetSdkRoslyn/csc.dll'
    mono = toolchain / 'MonoBleedingEdge/bin/mono.exe'
    references = toolchain / 'MonoBleedingEdge/lib/mono/4.8-api'
    for path in (dotnet, compiler, mono, framework):
        if not path.is_file():
            raise SystemExit(f'Missing required toolchain dependency: {path}')
    common = [str(dotnet), str(compiler), '-nologo', '-nostdlib+', '-langversion:latest', '-warnaserror+']
    common += [f'-r:{references / (name + ".dll")}' for name in ('mscorlib', 'System', 'System.Core')]

    def run(label, mutation=None):
        target = output / label
        target.mkdir(parents=True, exist_ok=True)
        shutil.copy2(framework, target / framework.name)
        sources = list(production)
        if mutation:
            old, new = mutation
            original = directory / 'Strongholds/StrongholdPlanner.cs'
            source = original.read_text(encoding='utf-8')
            if source.count(old) != 1:
                raise AssertionError(f'{label}: sabotage target is not unique')
            copied = target / original.name
            copied.write_text(source.replace(old, new), encoding='utf-8')
            sources[sources.index(original)] = copied
        library = target / 'AL.GameDataCatalog.dll'
        executable = target / 'tests.exe'
        commands = [
            common + ['-target:library', f'-out:{library}'] + list(map(str, sources)),
            common + ['-target:exe', f'-out:{executable}', f'-r:{framework}', f'-r:{library}',
                      str(tests), str(root / 'tools/strongholds/StrongholdTestRunner.cs')],
        ]
        for command in commands:
            completed = subprocess.run(command, cwd=root, capture_output=True, text=True, errors='replace', timeout=120)
            if completed.returncode:
                raise RuntimeError(completed.stdout + completed.stderr)
        completed = subprocess.run([str(mono), str(executable)], cwd=root, capture_output=True,
                                   text=True, errors='replace', timeout=120)
        transcript = completed.stdout + completed.stderr
        (target / 'results.txt').write_text(transcript, encoding='utf-8')
        print(f'[{label}]\n{transcript}', flush=True)
        return completed.returncode, transcript

    code, _ = run('current')
    if code:
        return code
    if args.sabotage:
        cases = {
            'early-deadline': (
                ('observation.TrustedTimeMilliseconds < state.Attempt.Deadline',
                 'observation.TrustedTimeMilliseconds < state.Attempt.Deadline - 1'),
                'StatueWaitsFull180SecondsAndFinalizationResetsCaptureAtomically'),
            'cancel-replaces-attempt': (
                ('"TakeoverCancelled", replaceAttempt: true',
                 '"TakeoverCancelled", attempt: new StrongholdAttempt(request.OperationId, request.ActorRealm, '
                 'observation.TrustedTimeMilliseconds, observation.TrustedTimeMilliseconds + catalog.TakeoverDurationMilliseconds, '
                 'state.OwnershipEpoch, state.Generation), replaceAttempt: true'),
                'RealmScopedAttemptKeepsDeadlineAndOtherRealmCancelsWithoutReplacement'),
            'stale-quote-accepted': (
                ('quote.Fingerprint != request.Quote.Fingerprint ||', ''),
                'CommandDefeatIsRequiredOnlyFromLevelFiveAndCaptureInvalidatesOldQuotes'),
        }
        for label, (mutation, expected_test) in cases.items():
            code, text = run(label, mutation)
            if code != 1 or 'FAIL ' + expected_test not in text:
                raise AssertionError(f'{label}: expected regression did not fail')
        code, _ = run('restored')
        if code:
            return code
        print(f'PASS: all {len(cases)} sabotage mutations caught; restored source green')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
