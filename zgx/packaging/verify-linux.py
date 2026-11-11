#!/usr/bin/env python3
"""Exercise DEB/RPM transactions in a disposable bwrap mount namespace (root)."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile
import xml.etree.ElementTree as ET


def run(*args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


parser = argparse.ArgumentParser()
parser.add_argument("dist", type=Path)
parser.add_argument("--version")
parser.add_argument("--release")
parser.add_argument("--inside", action="store_true")
args = parser.parse_args()
dist = args.dist.resolve()
if not args.version:
    latest = max(dist.glob("zg-console_*_amd64.deb"),
                 key=lambda p: tuple(map(int, p.name.split("_")[1].split("-")[0].split("."))))
    args.version = latest.name.split("_")[1].split("-")[0]
if not args.release:
    args.release = str(max(int(p.name.split("_")[1].rsplit("-", 1)[1])
                           for p in dist.glob(f"zg-console_{args.version}-*_amd64.deb")))
if os.geteuid() != 0:
    parser.error("run as root; only disposable directories are modified")
if not args.inside:
    with tempfile.TemporaryDirectory(prefix="zgshell-install-test-") as temp:
        root = Path(temp)
        shutil.copytree("/etc", root / "etc", symlinks=True)
        shutil.copytree("/var/lib/dpkg", root / "dpkg", symlinks=True)
        # Treat /opt as a shared system directory; it is a mountpoint in this test.
        with (root / "dpkg/info/base-files.list").open("a") as file:
            file.write("/opt\n/usr/share/metainfo\n")
        if Path("/usr/share/metainfo").exists():
            shutil.copytree("/usr/share/metainfo", root / "metainfo", symlinks=True)
        else:
            (root / "metainfo").mkdir()
        run("cp", "-a", "--reflink=auto", "/usr/bin", str(root / "bin"))
        for name in ("opt", "log", "tmp"):
            (root / name).mkdir()
        # Keep the mountpoint nonempty so dpkg does not try to remove /opt itself.
        (root / "opt/keep-mountpoint").touch()
        command = ["bwrap", "--die-with-parent", "--ro-bind", "/", "/",
                   "--dev", "/dev", "--proc", "/proc"]
        for source, target in (("etc", "/etc"), ("dpkg", "/var/lib/dpkg"),
                               ("bin", "/usr/bin"), ("opt", "/opt"),
                               ("log", "/var/log"), ("tmp", "/tmp"), ("tmp", "/var/tmp"),
                               ("metainfo", "/usr/share/metainfo")):
            command.extend(["--bind", str(root / source), target])
        run(*command, "python3", str(Path(__file__).resolve()), str(dist), "--inside", "--version", args.version, "--release", args.release)
else:
    profile = Path("/etc/profile.d/zipgenius-x.sh")
    baseline = "# unrelated profile entry preserved by installer tests\n"
    profile.write_text(baseline)
    deb = dist / f"zg-console_{args.version}-{args.release}_amd64.deb"
    rpm = dist / f"zg-console-{args.version}-{args.release}.x86_64.rpm"
    assert run("dpkg-deb", "-f", str(deb), "Package", capture_output=True, text=True).stdout.strip() == "zg-console"
    assert run("rpm", "-qp", "--qf", "%{NAME}", str(rpm), capture_output=True, text=True).stdout.strip() == "zg-console"
    for description in (run("dpkg-deb", "-f", str(deb), "Description", capture_output=True, text=True).stdout,
                        run("rpm", "-qp", "--qf", "%{DESCRIPTION}", str(rpm), capture_output=True, text=True).stdout):
        assert "ZipBomb protection is always active" in description
        assert "Self-contained .NET console" not in description
    app = "/opt/zipgenius/zgshell/zg"
    sample = Path("/tmp/installer-sample.zip")
    with zipfile.ZipFile(sample, "w") as archive:
        archive.writestr("sample.txt", "installer smoke test")

    def installed():
        metadata = ET.parse("/usr/share/metainfo/it.zipgenius.Console.metainfo.xml").getroot()
        assert metadata.findtext("pkgname") == "zg-console"
        assert metadata.findtext("name") == "ZipGenius X Console"
        assert metadata.findtext("developer/name") == "The ZipGenius Team"
        assert Path("/usr/bin/zg").resolve() == Path(app)
        assert not Path(app).parent.joinpath("man").exists(), 'Manuals must be embedded, not installed as loose files'
        startup = run(app, capture_output=True, text=True).stdout
        assert startup.splitlines()[0] == f"ZipGenius X Console - {args.version}"
        assert startup.count("ZipBomb") == 1
        hex_help = run(app, "-help", "-hexview", capture_output=True, text=True).stdout
        assert all(option in hex_help for option in ("zg -hexview", "FB=", "FT=", "BL="))
        assert not any(line.startswith(">>") for line in hex_help.splitlines())
        for culture, expected in (("it_IT.UTF-8", "Sintassi:"), ("en_US.UTF-8", "Syntax:")):
            environment = dict(os.environ, LANG=culture, LC_ALL=culture)
            translated = run(app, "-help", "-hexview", "--silent", env=environment,
                             capture_output=True, text=True).stdout
            assert expected in translated
            assert not any(line.startswith(">>") for line in translated.splitlines())
            add_help = run(app, "-help", "-add", "--silent", env=environment,
                           capture_output=True, text=True).stdout
            assert ("Descrizione:" if culture.startswith("it") else "Overview:") in add_help
            if culture.startswith("it"):
                assert "This command lets you" not in add_help
        assert profile.read_text().count("export PATH=") == 1
        assert baseline in profile.read_text()
        run(app, "-help", "-info", "--silent")
        run(app, "-test", str(sample), "--silent")
        run(app, "-info", str(sample), "--silent")
        run(app, "-sfx", "--list-targets", "--silent")
        denied = subprocess.run([app, "-install", "--system-wide", "--silent"],
                                user=65534, group=65534)
        # Re-registration succeeds without a write when entry exists.
        assert denied.returncode == 0
        denied = subprocess.run([app, "-uninstall", "--system-wide", "--silent"],
                                user=65534, group=65534)
        assert denied.returncode == 30, denied.returncode

    def removed():
        assert not Path(app).exists()
        assert not Path("/usr/share/metainfo/it.zipgenius.Console.metainfo.xml").exists()
        assert not Path("/usr/bin/zg").is_symlink()
        assert profile.read_text() == baseline

    run("dpkg", "-i", str(deb))
    installed()
    run("dpkg", "-i", str(deb))
    installed()
    run("dpkg", "--purge", "zg-console")
    removed()
    # Ubuntu is not an RPM system: bypass RPM distro dependency names only.
    # The runtime is actually executed against the host libraries above/below.
    rpm_flags = ("--dbpath", "/tmp/rpmdb", "--nodeps")
    run("rpm", *rpm_flags, "-i", str(rpm))
    installed()
    run("rpm", *rpm_flags, "-U", "--replacepkgs", str(rpm))
    installed()
    run("rpm", *rpm_flags, "-e", "zg-console")
    removed()
    # Exercise the package identity change using the latest legacy artifacts, when available.
    legacy_debs = list(dist.glob("zgshell_*_amd64.deb"))
    legacy_rpms = list(dist.glob("zgshell-*.x86_64.rpm"))
    if legacy_debs:
        legacy = max(legacy_debs, key=lambda p: tuple(map(int, p.name.split("_")[1].replace("-", ".").split("."))))
        run("dpkg", "-i", str(legacy))
        run("dpkg", "-i", str(deb))
        installed()
        run("dpkg", "--purge", "zg-console")
        removed()
        legacy_status = subprocess.run(["dpkg-query", "-W", "-f=${db:Status-Status}", "zgshell"], capture_output=True, text=True)
        assert legacy_status.stdout.strip() != "installed"
        print("PASS: DEB migration from zgshell to zg-console")
    if legacy_rpms:
        legacy = max(legacy_rpms, key=lambda p: tuple(map(int, p.name.removeprefix("zgshell-").removesuffix(".x86_64.rpm").replace("-", ".").split("."))))
        run("rpm", *rpm_flags, "-i", str(legacy))
        run("rpm", *rpm_flags, "-U", str(rpm))
        installed()
        run("rpm", *rpm_flags, "-e", "zg-console")
        removed()
        assert subprocess.run(["rpm", "--dbpath", "/tmp/rpmdb", "-q", "zgshell"], capture_output=True).returncode != 0
        print("PASS: RPM migration from zgshell to zg-console")
    print("PASS: DEB and RPM install, reinstall/upgrade, execution, permission errors and removal")
