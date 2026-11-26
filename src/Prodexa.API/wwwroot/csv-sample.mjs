// Read only a bounded prefix; stop after the header and twenty complete records.
export function parseCsvSample(text, complete = true, maxRows = 20) {
    const records = [];
    let fields = [];
    let field = "";
    let quoted = false;
    let closedQuote = false;
    let touched = false;

    function finishField() {
        fields.push(field.trim());
        field = "";
        closedQuote = false;
    }

    function finishRow() {
        if (touched || fields.length || field.trim()) {
            finishField();
            records.push(fields);
        }
        fields = [];
        field = "";
        closedQuote = false;
        touched = false;
    }

    if (text.charCodeAt(0) === 0xfeff) {
        text = text.slice(1);
    }

    for (let i = 0; i < text.length && records.length <= maxRows; i++) {
        const ch = text[i];
        if (quoted) {
            if (ch === '"') {
                if (text[i + 1] === '"') {
                    field += '"';
                    i++;
                } else {
                    quoted = false;
                    closedQuote = true;
                }
            } else {
                field += ch;
            }
            continue;
        }
        if (ch === ",") {
            touched = true;
            finishField();
        } else if (ch === "\n" || ch === "\r") {
            if (ch === "\r" && text[i + 1] === "\n") {
                i++;
            }
            finishRow();
        } else if (ch === '"' && !closedQuote && !field.trim()) {
            quoted = true;
            touched = true;
            field = "";
        } else if (closedQuote) {
            if (!/\s/.test(ch)) {
                throw new Error("Unexpected character after a quoted CSV field.");
            }
        } else {
            if (ch === '"') {
                throw new Error("Unexpected quote in an unquoted CSV field.");
            }
            field += ch;
            if (ch.trim()) {
                touched = true;
            }
        }
    }
    if (complete && records.length <= maxRows) {
        if (quoted) {
            throw new Error("Unclosed quoted CSV field.");
        }
        finishRow();
    }
    const columns = records.shift();
    if (!columns || columns.some(column => !column) || new Set(columns).size !== columns.length) {
        throw new Error("CSV headers must be nonempty and unique.");
    }
    const rows = records.slice(0, maxRows).map((row, index) => {
        if (row.length !== columns.length) {
            throw new Error("CSV row " + (index + 1) + " has a different column count.");
        }
        return Object.fromEntries(columns.map((column, position) => [column, row[position]]));
    });
    if (rows.length === 0) {
        throw new Error("No complete data rows in the sampled file prefix.");
    }
    return { columns, rows };
}

export async function readCsvSample(file) {
    const bytes = 1_048_576;
    const prefix = await file.slice(0, bytes).arrayBuffer();
    // The backend accepts UTF-8 (and BOM-marked UTF-16). Use the same BOM convention.
    const view = new Uint8Array(prefix);
    let encoding = "utf-8";
    if (view[0] === 0xff && view[1] === 0xfe) encoding = "utf-16le";
    if (view[0] === 0xfe && view[1] === 0xff) encoding = "utf-16be";
    const text = new TextDecoder(encoding, { fatal: true }).decode(prefix, { stream: file.size > bytes });
    return parseCsvSample(text, file.size <= bytes);
}
