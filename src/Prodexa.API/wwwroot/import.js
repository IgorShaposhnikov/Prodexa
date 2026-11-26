import { readCsvSample } from "./csv-sample.mjs";

const $ = id => document.getElementById(id);
const state = { targets: [], profiles: [], columns: [], rows: [], file: null, saved: null, busy: false };
const api = "/api/import";

function node(tag, text, className) {
    const element = document.createElement(tag);
    if (text !== undefined) element.textContent = text;
    if (className) element.className = className;
    return element;
}

function options(select, entries, selected = "") {
    select.replaceChildren();
    for (const [value, title] of entries) {
        select.append(new Option(title, value));
    }
    if (selected && !entries.some(entry => entry[0] === selected)) {
        select.append(new Option(selected + " (not in current file)", selected));
    }
    select.value = selected;
}

function columnOptions(select, selected = "", placeholder = "Choose a column") {
    options(select, [["", placeholder], ...state.columns.map(column => [column, column])], selected);
}

function message(text, error = false) {
    $("message").textContent = text;
    $("message").className = error ? "error" : "";
}

async function request(path, init = {}) {
    const response = await fetch(api + path, init);
    const text = await response.text();
    let value;
    try { value = JSON.parse(text); } catch { value = text; }
    if (!response.ok) {
        const detail = typeof value === "string" ? value : value.detail || value.title || JSON.stringify(value.errors || value);
        throw new Error(response.status + " — " + detail);
    }
    return value;
}

function jsonRequest(body) {
    return { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
}

function table(columns, rows) {
    const wrap = node("div", undefined, "table-scroll");
    const element = node("table");
    const head = node("thead");
    const header = node("tr");
    for (const column of columns) header.append(node("th", column));
    head.append(header);
    element.append(head);
    const body = node("tbody");
    for (const row of rows) {
        const tr = node("tr");
        for (const column of columns) {
            const cell = node("td", row[column] == null ? "—" : String(row[column]));
            cell.title = cell.textContent;
            tr.append(cell);
        }
        body.append(tr);
    }
    element.append(body);
    wrap.append(element);
    return wrap;
}

function fieldInput(label, control) {
    const element = node("label");
    element.append(node("span", label), control);
    return element;
}

function refreshProfiles(selected = "") {
    options($("profiles"), [["", "New profile"], ...state.profiles.map(profile => [profile.id, profile.name + " · " + profile.target])], selected);
}

function makeBinding(field, existing = {}) {
    const row = node("div", undefined, "binding");
    row.dataset.field = field.name;
    row.dataset.type = field.convertTo;
    row.dataset.required = String(field.required);

    const name = node("div");
    name.append(node("strong", field.name, "field-name"));
    name.append(node("span", field.convertTo + (field.required ? " · required" : " · optional"), "field-type" + (field.required ? " required" : "")));
    if (field.values?.length) name.append(node("span", field.values.join(" / "), "field-type"));
    row.append(name);

    const mode = node("select");
    mode.className = "mode";
    const modes = [["", "Not mapped"], ["column", "CSV column"], ["constant", "Constant"]];
    if (field.name === "id" && field.convertTo === "string") modes.push(["generate", "Generated ID"]);
    const selectedMode = existing.generate ? "generate" : existing.column ? "column" : existing.constant != null ? "constant" : "";
    options(mode, modes, selectedMode);
    row.append(fieldInput("Source", mode));

    const source = node("div", undefined, "rule");
    const column = node("select");
    column.className = "source-column";
    columnOptions(column, existing.column || "");
    const columnLabel = fieldInput("CSV column", column);
    source.append(columnLabel);
    const constant = field.values?.length || field.convertTo === "boolean" ? node("select") : node("input");
    constant.className = "constant";
    if (constant.tagName === "SELECT") {
        const values = field.values || ["true", "false"];
        options(constant, [["", "Choose value"], ...values.map(value => [value, value])], existing.constant == null ? "" : String(existing.constant));
    } else {
        constant.type = "text";
        constant.value = existing.constant == null ? "" : String(existing.constant);
        constant.placeholder = "Literal value";
    }
    const constantLabel = fieldInput("Constant", constant);
    source.append(constantLabel);
    const generated = node("p", "Stable ID from profile, dataset and source row.", "muted");
    source.append(generated);
    row.append(source);

    const rules = node("div", undefined, "rule");
    let formatLabel, equalsLabel;
    if (field.convertTo === "datetime_utc") {
        const format = node("select");
        format.className = "source-format";
        options(format, [["iso8601", "Date with UTC offset"], ["utc_naive", "Date without offset (UTC)"], ["unix_seconds", "Unix seconds"], ["unix_milliseconds", "Unix milliseconds"]], existing.sourceFormat || "iso8601");
        formatLabel = fieldInput("Source date format", format);
        rules.append(formatLabel);
    }
    if (field.convertTo === "boolean") {
        const equals = node("input");
        equals.className = "equals";
        equals.value = existing.equals ?? "";
        equals.placeholder = "e.g. scheduled_downtime";
        equalsLabel = fieldInput("True when equal to (optional)", equals);
        rules.append(equalsLabel);
    }
    row.append(rules);

    function updateMode() {
        columnLabel.hidden = mode.value !== "column";
        constantLabel.hidden = mode.value !== "constant";
        generated.hidden = mode.value !== "generate";
        if (formatLabel) formatLabel.hidden = !["column", "constant"].includes(mode.value);
        if (equalsLabel) equalsLabel.hidden = mode.value !== "column";
    }
    mode.addEventListener("change", updateMode);
    updateMode();
    return row;
}

function renderBindings(existing = {}) {
    const target = state.targets.find(item => item.target === $("target").value);
    const container = $("bindings");
    container.replaceChildren();
    $("target-hint").hidden = !target || target.target !== "Downtime";
    if (!$("target-hint").hidden) {
        $("target-hint").textContent = "Downtime references existing equipment. Import the Equipment target first if the machines are not yet in Prodexa.";
    }
    if (!target) {
        container.append(node("p", "Choose a target model to configure its fields.", "muted"));
        return;
    }
    const header = node("div", undefined, "binding-header");
    for (const label of ["Target field", "Mapping type", "Source value", "Conversion"]) header.append(node("span", label));
    container.append(header);
    for (const field of target.fields) {
        const binding = Object.hasOwn(existing, field.name) ? existing[field.name] : {};
        container.append(makeBinding(field, binding));
    }
}

function addFilter(column = "", values = []) {
    const row = node("div", undefined, "filter-row");
    const select = node("select");
    select.className = "filter-column";
    columnOptions(select, column);
    const input = node("textarea");
    input.className = "filter-values";
    input.rows = 2;
    input.placeholder = "downtime\nscheduled_downtime";
    input.value = values.join("\n");
    const remove = node("button", "Remove", "danger");
    remove.type = "button";
    remove.addEventListener("click", () => { row.remove(); changed(); });
    row.append(fieldInput("CSV column", select), fieldInput("Allowed values", input), remove);
    $("filters").append(row);
}

function definition(validate = false) {
    const mapping = {
        id: $("profile-id").value.trim(),
        name: $("profile-name").value.trim(),
        sourceSystem: $("source-system").value.trim(),
        target: $("target").value,
        sourceRevision: $("revision").value.trim(),
        filters: Object.create(null),
        fields: Object.create(null)
    };
    if (validate && (!mapping.id || !mapping.name || !mapping.sourceSystem || !mapping.target || !mapping.sourceRevision)) {
        throw new Error("Fill in the profile ID, name, source system, target and revision.");
    }
    if (validate && !/^[A-Za-z0-9_.-]+$/.test(mapping.id)) {
        throw new Error("Use letters, numbers, dots, underscores or hyphens for the profile ID.");
    }
    const sourceId = $("source-id").value;
    if (sourceId) mapping.sourceRecordIdColumn = sourceId;
    function checkColumn(column, field) {
        if (validate && (!column || (state.file && !state.columns.includes(column)))) {
            throw new Error("Select a valid CSV column for " + field + ".");
        }
    }
    if (sourceId) checkColumn(sourceId, "source record ID");
    for (const row of $("bindings").querySelectorAll(".binding")) {
        const field = row.dataset.field;
        const type = row.dataset.type;
        const mode = row.querySelector(".mode").value;
        if (!mode) {
            if (validate && row.dataset.required === "true") throw new Error("Required field " + field + " is not mapped.");
            continue;
        }
        const binding = { convertTo: type };
        if (mode === "column") {
            binding.column = row.querySelector(".source-column").value;
            checkColumn(binding.column, field);
            const equals = row.querySelector(".equals")?.value;
            if (equals) binding.equals = equals;
        } else if (mode === "constant") {
            const value = row.querySelector(".constant").value;
            if (validate && !value && type !== "string") throw new Error("Specify a constant for " + field + ".");
            // Preserve the text as entered, especially decimal precision. The server performs typed conversion.
            binding.constant = value;
        } else {
            binding.generate = "stable_id";
        }
        if (row.querySelector(".source-format") && mode !== "generate") {
            binding.sourceFormat = row.querySelector(".source-format").value;
        }
        mapping.fields[field] = binding;
    }
    for (const row of $("filters").querySelectorAll(".filter-row")) {
        const column = row.querySelector(".filter-column").value;
        const values = row.querySelector(".filter-values").value.split(/\r?\n/).filter(value => value !== "");
        checkColumn(column, "filter");
        if (validate && !values.length) throw new Error("Enter at least one allowed value for the filter.");
        if (column) {
            if (validate && Object.hasOwn(mapping.filters, column)) throw new Error("Only one filter per column is allowed.");
            mapping.filters[column] = values;
        }
    }
    return mapping;
}

function signature(mapping) {
    function normalize(value) {
        if (Array.isArray(value)) return value.map(normalize);
        if (value && typeof value === "object") {
            return Object.fromEntries(Object.keys(value).sort().map(key => [key, normalize(value[key])]));
        }
        return value;
    }
    return JSON.stringify(normalize(mapping));
}

function changed() {
    const current = definition();
    $("definition").textContent = JSON.stringify(current, null, 2);
    const saved = state.saved && signature(current) === state.saved.signature;
    $("save").disabled = state.busy || !state.targets.length || !!saved;
    $("preview").disabled = state.busy || !state.file || !state.rows.length || !state.targets.length;
    $("import").disabled = state.busy || !state.file || !saved;
    $("preview-result").hidden = true;
    $("import-result").hidden = true;
}

function loadProfile(profile) {
    state.saved = null;
    $("profile-id").value = profile?.id || "";
    $("profile-name").value = profile?.name || "";
    $("source-system").value = profile?.sourceSystem || "";
    $("target").value = profile?.target || "";
    $("revision").value = profile?.sourceRevision || "1";
    columnOptions($("source-id"), profile?.sourceRecordIdColumn || "", "Generated from dataset and row");
    renderBindings(profile?.fields || {});
    $("filters").replaceChildren();
    for (const [column, values] of Object.entries(profile?.filters || {})) addFilter(column, values);
    if (profile) state.saved = { id: profile.id, signature: signature(definition()) };
    changed();
}

function updateColumns() {
    columnOptions($("source-id"), $("source-id").value, "Generated from dataset and row");
    for (const select of document.querySelectorAll(".source-column, .filter-column")) {
        columnOptions(select, select.value);
    }
}

function dataset() {
    const value = $("dataset").value.trim();
    if (!value) throw new Error("Enter a stable dataset ID.");
    return value;
}

async function action(callback) {
    if (state.busy) return;
    state.busy = true;
    const controls = Array.from(document.querySelectorAll("main input, main select, main textarea, main button"));
    const previous = controls.map(control => control.disabled);
    controls.forEach(control => { control.disabled = true; });
    try {
        await callback();
    } catch (error) {
        message(error.message || String(error), true);
    } finally {
        state.busy = false;
        controls.forEach((control, index) => { control.disabled = previous[index]; });
        const current = definition();
        const saved = state.saved && signature(current) === state.saved.signature;
        $("save").disabled = !state.targets.length || !!saved;
        $("preview").disabled = !state.file || !state.rows.length || !state.targets.length;
        $("import").disabled = !state.file || !saved;
    }
}

function metrics(result, fields) {
    const container = node("div", undefined, "metrics");
    for (const [key, label] of fields) {
        const metric = node("div", undefined, "metric");
        metric.append(node("span", label), node("strong", Number(result[key]).toLocaleString()));
        container.append(metric);
    }
    return container;
}

$("file").addEventListener("change", async () => {
    const selected = $("file").files[0];
    await action(async () => {
        state.file = null;
        state.columns = [];
        state.rows = [];
        $("source-table").hidden = true;
        $("source-summary").textContent = "";
        $("file-label").textContent = selected?.name || "Choose a CSV file";
        $("file-info").textContent = selected ? (selected.size / 1_000_000).toFixed(2) + " MB · local file" : "UTF-8, comma separated · up to 75 MB";
        updateColumns();
        changed();
        if (!selected) return;
        if (selected.size === 0 || selected.size > 75_000_000) throw new Error("Choose a nonempty CSV no larger than 75 MB.");
        const sample = await readCsvSample(selected);
        state.file = selected;
        state.columns = sample.columns;
        state.rows = sample.rows;
        updateColumns();
        $("source-summary").textContent = state.columns.length + " columns · " + state.rows.length + " sample rows · not uploaded";
        $("source-table").replaceChildren(table(state.columns, state.rows));
        $("source-table").hidden = false;
        if (!$("dataset").value) $("dataset").value = selected.name.replace(/\.csv$/i, "").slice(0, 100);
        changed();
        message("CSV header and sample read locally.");
    });
});

$("target").addEventListener("change", () => { renderBindings(); changed(); });
$("profiles").addEventListener("change", () => {
    loadProfile(state.profiles.find(profile => profile.id === $("profiles").value));
    message(state.saved ? "Saved profile loaded. To change it, use a new profile ID." : "New profile.");
});
$("new-profile").addEventListener("click", () => {
    $("profiles").value = "";
    loadProfile(null);
    message("New profile.");
});
$("add-filter").addEventListener("click", () => { addFilter(); changed(); });
document.querySelector("main").addEventListener("input", event => {
    if (event.target.id !== "file" && event.target.id !== "profiles" && event.target.id !== "target") changed();
});
document.querySelector("main").addEventListener("change", event => {
    if (event.target.id !== "file" && event.target.id !== "profiles" && event.target.id !== "target") changed();
});
$("preview").addEventListener("click", () => action(async () => {
    const mapping = definition(true);
    const payload = jsonRequest({ mapping, datasetId: dataset(), rows: state.rows });
    if (new TextEncoder().encode(payload.body).length > 131_072) throw new Error("The first 20 rows exceed the API preview limit (128 KiB).");
    message("Validating sample with the backend…");
    const result = await request("/mappings/preview", payload);
    const container = $("preview-result");
    const rows = result.records.map(record => record.data);
    const columns = [...new Set(rows.flatMap(row => Object.keys(row)))];
    container.replaceChildren(node("h3", "Converted preview"), metrics(result, [["rowsScanned", "Sample rows"], ["filteredRows", "Filtered"]]));
    if (rows.length) container.append(table(columns, rows));
    else container.append(node("p", "No sample rows matched the filters."));
    container.hidden = false;
    message("Preview complete. No data or profile was saved.");
}));
$("save").addEventListener("click", () => action(async () => {
    const mapping = definition(true);
    if (state.profiles.some(profile => profile.id === mapping.id)) throw new Error("This profile ID already exists. Load it or choose a new ID for an edited profile.");
    const saved = await request("/mappings", jsonRequest(mapping));
    state.profiles.push(saved);
    refreshProfiles(saved.id);
    loadProfile(saved);
    message("Profile saved. No CSV records have been imported.");
}));
$("import").addEventListener("click", () => action(async () => {
    const mapping = definition(true);
    const id = dataset();
    if (!state.saved || signature(mapping) !== state.saved.signature) throw new Error("Save the mapping first.");
    if (!state.file) throw new Error("Choose a CSV file.");
    const body = new FormData();
    body.append("file", state.file);
    message("Importing full CSV. Keep this page open until the server responds.");
    const result = await request("/mappings/" + encodeURIComponent(state.saved.id) + "/import?datasetId=" + encodeURIComponent(id), { method: "POST", body });
    const container = $("import-result");
    container.replaceChildren(node("h3", "Import result"), metrics(result, [["rowsScanned", "Rows scanned"], ["filteredRows", "Filtered"], ["importedCount", "Imported"], ["skippedCount", "Unchanged / skipped"]]));
    container.hidden = false;
    message("Import completed.");
}));

async function initialize() {
    await action(async () => {
        [state.targets, state.profiles] = await Promise.all([request("/targets"), request("/mappings")]);
        options($("target"), [["", "Choose a model"], ...state.targets.map(target => [target.target, target.target])]);
        refreshProfiles();
        $("connection").textContent = "API connected";
        changed();
    });
    if (!state.targets.length) {
        $("connection").textContent = "API unavailable";
        $("connection").classList.add("error");
    }
}

initialize();
