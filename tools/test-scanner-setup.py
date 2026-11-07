#!/usr/bin/env python3
import importlib.util
import pathlib
import sys
import tempfile
import unittest
from unittest.mock import patch

module_spec = importlib.util.spec_from_file_location("scanner_setup", pathlib.Path(__file__).with_name("scanner_setup.py"))
setup = importlib.util.module_from_spec(module_spec)
sys.modules[module_spec.name] = setup
module_spec.loader.exec_module(setup)


class ScannerSetupTests(unittest.TestCase):
    def test_supported_package_plans_and_loopback_limits(self):
        for system, distro in [("Linux", "fedora"), ("Linux", "debian"), ("Linux", "ubuntu"), ("FreeBSD", "")]:
            with self.subTest(system=system, distro=distro):
                plan = setup.plan_for(system, distro)
                values = setup.daemon_values(plan)
                self.assertEqual("127.0.0.1", values["TCPAddr"])
                self.assertEqual("3310", values["TCPSocket"])
                self.assertEqual(536870912, int(values["StreamMaxLength"]))
                self.assertEqual("yes", values["AlertExceedsMax"])
                self.assertEqual("yes", values["AlertEncrypted"])
                self.assertTrue(plan.packages)
        with self.assertRaises(RuntimeError):
            setup.plan_for("Linux", "unknown")

    def test_patch_is_idempotent_and_keeps_unrelated_settings(self):
        original = "# vendor comment\nExample\nTCPAddr 0.0.0.0\nTCPAddr ::\nTCPSocket 1\nLogTime yes\n"
        values = {"TCPAddr": "127.0.0.1", "TCPSocket": "3310"}
        result = setup.patch_text(original, values)
        self.assertEqual(result, setup.patch_text(result, values))
        self.assertIn("LogTime yes", result)
        self.assertIn("# vendor comment", result)
        self.assertNotIn("0.0.0.0", result)
        self.assertNotIn("\nExample\n", result)
        self.assertEqual(1, result.count("TCPAddr "))

    def test_backups_are_private_and_links_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            config = pathlib.Path(directory) / "clamd.conf"
            config.write_text("TCPAddr 0.0.0.0\n")
            setup.patch_file(config, {"TCPAddr": "127.0.0.1"})
            backup = next(pathlib.Path(directory).glob("*.appsouls-backup-*"))
            self.assertEqual(0o600, backup.stat().st_mode & 0o777)
            self.assertIn("0.0.0.0", backup.read_text())
            link = pathlib.Path(directory) / "linked.conf"
            link.symlink_to(config)
            with self.assertRaises(RuntimeError):
                setup.patch_file(link, {"TCPAddr": "bad"})
            self.assertIn("127.0.0.1", config.read_text())

    def test_responding_external_scanner_is_reused_without_package_or_config_changes(self):
        with patch.object(setup, "ping", return_value=True), patch.object(setup, "run") as run, patch.object(setup, "patch_file") as configure:
            setup.install(setup.plan_for("Linux", "fedora"))
            run.assert_not_called()
            configure.assert_not_called()

    def test_fresh_install_updates_definitions_and_enables_both_services(self):
        plan = setup.plan_for("Linux", "fedora")
        with patch.object(setup, "ping", side_effect=[False, True]), patch.object(setup.os, "geteuid", return_value=0), patch.object(setup.shutil, "which", return_value="/usr/bin/tool"), patch.object(setup, "patch_file") as configure, patch.object(setup, "run") as run:
            setup.install(plan)
            commands = [tuple(call.args[0]) for call in run.call_args_list]
            self.assertIn(("dnf", "install", "-y", "clamav", "clamd", "clamav-freshclam"), commands)
            self.assertIn(("freshclam", "--config-file=/etc/freshclam.conf"), commands)
            self.assertIn(("systemctl", "enable", "clamd@scan", "clamav-freshclam"), commands)
            self.assertIn(("systemctl", "restart", "clamd@scan"), commands)
            self.assertEqual(2, configure.call_count)

    def test_failed_definition_update_attempts_to_resume_updater_and_never_claims_ready(self):
        def command(args, check=True):
            if args[0] == "freshclam":
                raise setup.subprocess.CalledProcessError(1, args)
        with patch.object(setup, "ping", return_value=False), patch.object(setup.os, "geteuid", return_value=0), patch.object(setup.shutil, "which", return_value="/usr/bin/tool"), patch.object(setup, "patch_file"), patch.object(setup, "run", side_effect=command) as run:
            with self.assertRaises(setup.subprocess.CalledProcessError):
                setup.install(setup.plan_for("Linux", "fedora"))
            commands = [tuple(call.args[0]) for call in run.call_args_list]
            self.assertIn(("systemctl", "start", "clamav-freshclam"), commands)
            self.assertNotIn(("systemctl", "restart", "clamd@scan"), commands)


if __name__ == "__main__":
    unittest.main()
