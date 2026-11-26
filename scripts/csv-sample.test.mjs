import { test } from "node:test";
import assert from "node:assert/strict";
import { parseCsvSample, readCsvSample } from "../src/Prodexa.API/wwwroot/csv-sample.mjs";

test("quoted commas, escaped quotes, multiline fields and a BOM", () => {
    const sample = parseCsvSample('\ufeffid,name,note\r\n1,"Machine, A","said ""ready"""\r\n2,B,"first\nsecond"\r\n');
    assert.deepEqual(sample.columns, ["id", "name", "note"]);
    assert.deepEqual(sample.rows, [
        { id: "1", name: "Machine, A", note: 'said "ready"' },
        { id: "2", name: "B", note: "first\nsecond" }
    ]);
});

test("sample is limited to twenty rows and ignores an incomplete final record", () => {
    const data = "id,kind\n" + Array.from({ length: 30 }, (_, index) => index + ",downtime\n").join("");
    assert.equal(parseCsvSample(data).rows.length, 20);
    assert.deepEqual(parseCsvSample('id,kind\n1,ok\n2,"incomplete', false).rows, [{ id: "1", kind: "ok" }]);
});

test("rejects malformed or duplicate headers and incorrect column counts", () => {
    assert.throws(() => parseCsvSample("id,id\n1,2"), /unique/);
    assert.throws(() => parseCsvSample("id,type\n1"), /column count/);
    assert.throws(() => parseCsvSample('id,type\n1,"unfinished'), /Unclosed/);
});

test("sample reader only requests the first MiB of a large file", async () => {
    const data = new TextEncoder().encode("machine_id\nM01\nM02\n");
    const file = {
        size: 48_000_000,
        slice(start, end) {
            assert.equal(start, 0);
            assert.equal(end, 1_048_576);
            return { arrayBuffer: async () => data.buffer };
        }
    };
    const sample = await readCsvSample(file);
    assert.deepEqual(sample.rows, [{ machine_id: "M01" }, { machine_id: "M02" }]);
});
