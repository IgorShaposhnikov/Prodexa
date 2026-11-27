#!/usr/bin/env python3
"""Load the reproducible Prodexa dataset and verify the three analytic scenarios."""

import argparse
import json
import sys
from decimal import Decimal
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


def request_json(url, payload=None):
    headers = {"Accept": "application/json"}
    body = None
    method = "GET"

    if payload is not None:
        headers["Content-Type"] = "application/json"
        body = json.dumps(payload).encode("utf-8")
        method = "POST"

    request = Request(url, data=body, headers=headers, method=method)

    with urlopen(request, timeout=30) as response:
        return json.load(response, parse_float=Decimal)


def find_record(records, key, value):
    matches = [item for item in records if item[key] == value]

    if len(matches) != 1:
        raise AssertionError(f"Expected one {key}={value}, found {len(matches)}.")

    return matches[0]


def run_demo(base_url):
    sample_path = Path(__file__).resolve().parent.parent / "samples" / "demo-batch.json"
    batch = json.loads(sample_path.read_text(encoding="utf-8"))

    imported = request_json(f"{base_url}/api/import/records", batch)
    total = len(batch["records"])

    if imported["importedCount"] + imported["skippedCount"] != total:
        raise AssertionError("The import did not process every demo record.")

    # A second import must be a no-op, even when the database is reused.
    replay = request_json(f"{base_url}/api/import/records", batch)
    if replay["importedCount"] != 0 or replay["skippedCount"] != total:
        raise AssertionError("The repeated import was not idempotent.")

    as_of = quote("2026-11-01T12:00:00+05:00", safe="")
    period_start = quote("2026-11-01T08:00:00+05:00", safe="")
    overdue = request_json(f"{base_url}/api/production/overdue-tasks?asOf={as_of}")
    downtime = request_json(
        f"{base_url}/api/equipment/EQ-01/downtime?periodStart={period_start}&periodEnd={as_of}"
    )
    materials = request_json(f"{base_url}/api/materials/consumption-variance?asOf={as_of}")

    production = find_record(overdue, "taskId", "J-01")
    if production["acceptedGoodOutput"] != 60 or production["remainingQuantity"] != 40:
        raise AssertionError("UC01 produced unexpected quantities.")
    if production["overdueMinutes"] != 120:
        raise AssertionError("UC01 produced an unexpected overdue duration.")
    if any(item["taskId"] == "J-02" for item in overdue):
        raise AssertionError("Completed production was unexpectedly overdue.")

    if downtime["eventCount"] != 2 or downtime["downtimeMinutes"] != 60:
        raise AssertionError("UC02 incorrectly combined equipment downtime.")
    if downtime["unplannedDowntimeMinutes"] != 60 or downtime["plannedDowntimeMinutes"] != 0:
        raise AssertionError("UC02 incorrectly separated planned and unplanned downtime.")
    if downtime["availabilityPercent"] != 75:
        raise AssertionError("UC02 produced an unexpected availability percentage.")

    consumption = find_record(materials, "taskId", "J-02")
    if consumption["acceptedOutput"] != 50 or consumption["expectedConsumption"] != 100:
        raise AssertionError("UC03 produced unexpected production or norm quantities.")
    if consumption["netConsumption"] != 110 or consumption["varianceQuantity"] != 10:
        raise AssertionError("UC03 produced an unexpected consumption variance.")
    if consumption["variancePercent"] != 10:
        raise AssertionError("UC03 produced an unexpected percentage.")

    print(f"Imported: {imported['importedCount']}, skipped: {imported['skippedCount']}")
    print(f"Replay: {replay['skippedCount']} unchanged source revisions")
    print("UC01: J-01 has 40 pieces remaining and is 120 minutes overdue")
    print("UC02: EQ-01 unplanned downtime is 60 minutes; availability is 75%")
    print("UC03: J-02 net material usage is 110 kg; overconsumption is 10 kg (10%)")
    print("All demo scenarios passed.")


def main():
    parser = argparse.ArgumentParser(description="Import and verify the Prodexa demo dataset.")
    parser.add_argument(
        "--url",
        default="http://localhost:5080",
        help="Base URL of the running Prodexa API (default: http://localhost:5080)",
    )
    args = parser.parse_args()

    try:
        run_demo(args.url.rstrip("/"))
    except (HTTPError, URLError, AssertionError, ValueError) as error:
        if isinstance(error, HTTPError):
            message = error.read().decode("utf-8", errors="replace")
            print(f"HTTP {error.code}: {message}", file=sys.stderr)
        else:
            print(f"Demo failed: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
