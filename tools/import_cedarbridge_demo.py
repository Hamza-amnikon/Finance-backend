"""Import the fictional Cedarbridge workbook through the existing finance APIs.

Run each stage separately. The state file records IDs so a stopped import can resume.
"""

import argparse
import datetime as dt
import json
import re
import ssl
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


WORKBOOK = Path(r"C:\Users\2020023\Downloads\Company_Accounts_Demo.xlsx")
STATE = Path(__file__).with_name("cedarbridge_demo_state.json")
MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG = "{http://schemas.openxmlformats.org/package/2006/relationships}"
UNVERIFIED_LOCAL_CERT = ssl._create_unverified_context()


def workbook_rows():
    with zipfile.ZipFile(WORKBOOK) as archive:
        strings = []
        if "xl/sharedStrings.xml" in archive.namelist():
            root = ET.fromstring(archive.read("xl/sharedStrings.xml"))
            strings = ["".join(item.itertext()) for item in root.findall(MAIN + "si")]
        book = ET.fromstring(archive.read("xl/workbook.xml"))
        rels = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
        paths = {item.attrib["Id"]: item.attrib["Target"].lstrip("/") for item in rels.findall(PKG + "Relationship")}
        result = {}
        for sheet in book.find(MAIN + "sheets"):
            path = paths[sheet.attrib[REL + "id"]]
            if not path.startswith("xl/"):
                path = "xl/" + path
            root = ET.fromstring(archive.read(path))
            rows = []
            for row in root.iter(MAIN + "row"):
                values = {}
                for cell in row.findall(MAIN + "c"):
                    column = re.match(r"[A-Z]+", cell.attrib["r"]).group()
                    value = cell.find(MAIN + "v")
                    inline = cell.find(MAIN + "is")
                    raw = value.text if value is not None else "".join(inline.itertext()) if inline is not None else ""
                    values[column] = strings[int(raw)] if cell.attrib.get("t") == "s" else raw
                rows.append(values)
            result[sheet.attrib["name"]] = rows[5:]
        return result


def date(value):
    return (dt.date(1899, 12, 30) + dt.timedelta(days=int(value))).isoformat()


def request(port, path, payload=None, http=False):
    scheme = "http" if http else "https"
    body = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(
        f"{scheme}://localhost:{port}{path}",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST" if body is not None else "GET",
    )
    try:
        with urllib.request.urlopen(req, context=UNVERIFIED_LOCAL_CERT, timeout=45) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(f"{req.full_url}: HTTP {error.code}: {error.read().decode()[:600]}") from error


def save(state):
    STATE.write_text(json.dumps(state, indent=2), encoding="utf-8")


def import_contacts(rows, state):
    state.setdefault("clients", {})
    state.setdefault("vendors", {})
    for row in rows["Contacts"]:
        key = row.get("A", "")
        if not key:
            continue
        company = row["C"]
        if row["B"] == "Customer":
            if key in state["clients"]:
                continue
            value = request(5080, "/api/clients", {
                "name": company, "companyName": company, "email": row["D"],
                "address": row["E"], "branch": "Dubai", "currency": "USD",
                "notes": f"SAMPLE - LAB DEMO ONLY; Cedarbridge workbook contact {key}",
            }, http=True)
            state["clients"][key] = value["id"]
        elif row["B"] == "Supplier":
            if key in state["vendors"]:
                continue
            value = request(7179, "/api/vendors", {
                "name": company, "companyName": company, "email": row["D"],
                "address": row["E"], "branch": "Dubai", "currency": "USD",
                "notes": f"SAMPLE - LAB DEMO ONLY; Cedarbridge workbook contact {key}",
            })
            state["vendors"][key] = value["id"]
        save(state)
        print(key, company)


def import_invoices(rows, state, limit):
    contacts = {row["C"]: row["A"] for row in rows["Contacts"] if row.get("B") == "Customer"}
    lines = {}
    for row in rows["Document Lines"]:
        if row.get("A") == "Invoice":
            lines.setdefault(row["B"], []).append(row)
    state.setdefault("invoices", {})
    done = 0
    for row in rows["Sales Invoices"]:
        source_id = row.get("A", "")
        if not source_id or source_id in state["invoices"]:
            continue
        items = []
        for line in lines[source_id]:
            items.append({
                "serviceName": line["D"], "description": line["D"],
                "unit": 0 if line["F"] == "hours" else 3,
                "quantity": float(line["E"]), "unitPrice": float(line["G"]),
                "discountEnabled": False, "discountAmount": 0, "taxRate": 0,
            })
        value = request(7173, "/api/sales-invoices", {
            "clientId": state["clients"][contacts[row["D"]]],
            "invoiceDate": date(row["B"]), "dueDate": date(row["C"]),
            "currency": "USD", "branch": "Dubai", "taxTreatment": "NoTax",
            "reference": source_id,
            "notes": f"SAMPLE - LAB DEMO ONLY; source invoice {source_id}",
            "items": items,
        })
        state["invoices"][source_id] = {"id": value["id"], "number": value.get("invoiceNumber"), "status": "Draft"}
        save(state)
        print("Draft invoice", source_id, value["id"])
        issued = request(7173, f"/api/sales-invoices/{value['id']}/issue", {"rowVersion": value["rowVersion"]})
        state["invoices"][source_id] = {"id": issued["id"], "number": issued.get("invoiceNumber"), "status": "Issued"}
        save(state)
        print("Issued invoice", source_id, issued.get("invoiceNumber"))
        done += 1
        if limit and done >= limit:
            break


def import_expense_accounts(state):
    state.setdefault("expense_accounts", {})
    for code, name in (("5270", "Cloud Hosting Expense"), ("5280", "Office Supplies Expense"), ("5290", "Petty Cash Expense")):
        if code in state["expense_accounts"]:
            continue
        value = request(7017, "/api/accounts", {
            "companyId": 1, "code": code, "name": name,
            "type": "Expense", "normalBalance": "Debit",
            "parentAccountId": 116, "isPostingAccount": True,
            "description": "SAMPLE - LAB DEMO ONLY; Cedarbridge workbook expense category",
        })
        state["expense_accounts"][code] = value["id"]
        save(state)
        print("Expense account", code, name)


def import_bills(rows, state, limit):
    contacts = {row["C"]: row["A"] for row in rows["Contacts"] if row.get("B") == "Supplier"}
    lines = {}
    for row in rows["Document Lines"]:
        if row.get("A") == "Bill":
            lines.setdefault(row["B"], []).append(row)
    state.setdefault("bills", {})
    done = 0
    for row in rows["Supplier Bills"]:
        source_id = row.get("A", "")
        if not source_id or source_id in state["bills"]:
            continue
        items = []
        for line in lines[source_id]:
            name = line["D"]
            code = "5220" if "workspace" in name.lower() else "5270" if "cloud" in name.lower() else "5280"
            items.append({
                "itemName": name, "description": name, "accountCode": code,
                "unit": 2 if line["F"] == "month" else 4,
                "quantity": float(line["E"]), "unitPrice": float(line["G"]),
                "discountEnabled": False, "discountAmount": 0, "taxRate": 0,
            })
        value = request(7193, "/api/bills", {
            "requestId": str(uuid.uuid5(uuid.NAMESPACE_URL, "cedarbridge-demo-bill-" + source_id)),
            "vendorInvoiceNumber": row["B"],
            "vendorId": state["vendors"][contacts[row["E"]]],
            "billDate": date(row["C"]), "dueDate": date(row["D"]),
            "branch": "Dubai", "currency": "USD", "taxTreatment": "NoTax",
            "reference": source_id,
            "notes": f"SAMPLE - LAB DEMO ONLY; source bill {source_id}",
            "items": items,
        })
        state["bills"][source_id] = {"id": value["id"], "number": value.get("billNumber"), "status": "Draft"}
        save(state)
        print("Draft bill", source_id, value["id"])
        issued = request(7193, f"/api/bills/{value['id']}/issue", {"rowVersion": value["rowVersion"]})
        state["bills"][source_id] = {"id": issued["id"], "number": issued.get("billNumber"), "status": "Issued"}
        save(state)
        print("Issued bill", source_id, issued.get("billNumber"))
        done += 1
        if limit and done >= limit:
            break


def import_receipts(rows, state, limit):
    state.setdefault("receipts", {})
    done = 0

    for row in rows["Settlements"]:
        source_id = row.get("A", "")
        if row.get("C") != "Customer receipt" or source_id in state["receipts"]:
            continue

        invoice = state["invoices"][row["D"]]
        amount = float(row["G"])
        value = request(7173, f"/api/sales-invoices/{invoice['id']}/payments", {
            "requestId": str(uuid.uuid5(uuid.NAMESPACE_URL, "cedarbridge-demo-receipt-" + source_id)),
            "paymentDate": date(row["B"]),
            "amount": amount,
            "paymentMode": "NEFT",
            "reference": source_id,
            "notes": f"SAMPLE - LAB DEMO ONLY; source receipt {source_id}",
            "bankAccountId": 1002,
            "bankCurrency": "USD",
            "bankAmount": amount,
            "bankChargeAmount": 0,
        })
        state["receipts"][source_id] = value["id"]
        save(state)
        print("Receipt", source_id, value["id"])
        done += 1
        if limit and done >= limit:
            break


def import_supplier_payments(rows, state, limit):
    state.setdefault("supplier_payments", {})
    done = 0

    for row in rows["Settlements"]:
        source_id = row.get("A", "")
        if row.get("C") != "Supplier payment" or source_id in state["supplier_payments"]:
            continue

        bill_id = state["bills"][row["D"]]["id"]
        bill = request(7193, f"/api/bills/{bill_id}")
        amount = float(row["G"])
        value = request(5231, "/api/payments-made", {
            "requestId": str(uuid.uuid5(uuid.NAMESPACE_URL, "cedarbridge-demo-supplier-payment-" + source_id)),
            "vendorId": bill["vendorId"],
            "billId": bill_id,
            "paymentDate": date(row["B"]),
            "amount": amount,
            "paymentModeId": 2,
            "reference": source_id,
            "notes": f"SAMPLE - LAB DEMO ONLY; source supplier payment {source_id}",
            "billRowVersion": bill["rowVersion"],
            "bankAccountId": 1002,
            "bankCurrency": "USD",
            "bankAmount": amount,
        }, http=True)
        state["supplier_payments"][source_id] = value["id"]
        save(state)
        print("Supplier payment", source_id, value["id"])
        done += 1
        if limit and done >= limit:
            break


def import_bank_adjustments(rows, state, limit):
    state.setdefault("bank_adjustments", {})
    done = 0
    for row in rows["Bank Statement"]:
        if row.get("C") not in ("Cash withdrawal", "Bank fee"):
            continue
        source_id = row["D"]
        saved = state["bank_adjustments"].get(source_id)
        if saved and saved["status"] == "Posted":
            continue
        if saved:
            value = request(7051, f"/api/bank-transactions/{saved['id']}")
        else:
            value = request(7051, "/api/bank-transactions", {
                "requestId": str(uuid.uuid5(uuid.NAMESPACE_URL, "cedarbridge-demo-bank-" + source_id)),
                "bankAccountId": 1002,
                "transactionDate": date(row["B"]),
                "direction": 2,
                "amount": float(row["I"]),
                "reference": source_id,
                "description": row["G"],
                "notes": f"SAMPLE - LAB DEMO ONLY; source bank row {row['A']}",
            })
            state["bank_adjustments"][source_id] = {"id": value["id"], "status": "Draft"}
            save(state)
        if value["status"] != "Posted":
            value = request(7051, f"/api/bank-transactions/{value['id']}/post", {
                "rowVersion": value["rowVersion"],
            })
        state["bank_adjustments"][source_id] = {"id": value["id"], "status": "Posted"}
        save(state)
        print("Posted bank adjustment", source_id, value["id"])
        done += 1
        if limit and done >= limit:
            break


def import_opening_equity_account(state):
    if "opening_equity_account" in state:
        return
    accounts = request(7017, "/api/accounts?CompanyId=1&PageSize=100")["data"]
    existing = next((row for row in accounts if row["code"] == "3400"), None)
    value = existing or request(7017, "/api/accounts", {
        "companyId": 1,
        "code": "3400",
        "name": "Demo Opening Equity",
        "type": "Equity",
        "normalBalance": "Credit",
        "parentAccountId": 107,
        "isPostingAccount": True,
        "description": "SAMPLE - LAB DEMO ONLY; opening equity in Cedarbridge workbook",
    })
    state["opening_equity_account"] = value["id"]
    save(state)
    print("Opening equity account", value["id"])


def journal_source_rows(rows, state):
    yield "OPEN-001", "2026-04-01", "Opening USD bank balance", [
        (123, 25000, 0, "Opening USD bank balance"),
        (state["opening_equity_account"], 0, 25000, "Opening equity"),
    ]
    yield "OPEN-002", "2026-04-01", "Opening petty cash balance", [
        (87, 500, 0, "Opening petty cash balance"),
        (state["opening_equity_account"], 0, 500, "Opening equity"),
    ]
    for row in rows["Bank Statement"]:
        if row.get("C") not in ("Cash withdrawal", "Bank fee"):
            continue
        source_id = row["D"]
        amount = float(row["I"])
        debit_account = 87 if row["C"] == "Cash withdrawal" else 121
        yield source_id, date(row["B"]), row["G"], [
            (debit_account, amount, 0, row["G"]),
            (123, 0, amount, "USD bank movement " + source_id),
        ]
    for row in rows["Cashbook"]:
        if row.get("B") != "Petty cash expense":
            continue
        source_id = row["C"]
        amount = float(row["F"])
        yield source_id, date(row["A"]), row["D"], [
            (state["expense_accounts"]["5290"], amount, 0, row["D"]),
            (87, 0, amount, "Petty cash spent " + source_id),
        ]


def import_manual_journals(rows, state, limit):
    import_opening_equity_account(state)
    state.setdefault("manual_journals", {})
    rates = {}
    done = 0
    for source_id, entry_date, narration, source_lines in journal_source_rows(rows, state):
        saved = state["manual_journals"].get(source_id)
        if saved and saved["status"] == "Posted":
            continue
        if saved:
            value = request(7017, f"/api/journal-entries/{saved['id']}?companyId=1")
        else:
            if entry_date not in rates:
                rates[entry_date] = request(7442, f"/api/exchange-rates/rate/USD/AED?date={entry_date}", http=True)["rate"]
            value = request(7017, "/api/journal-entries", {
                "requestId": str(uuid.uuid5(uuid.NAMESPACE_URL, "cedarbridge-demo-journal-" + source_id)),
                "companyId": 1,
                "entryDate": entry_date,
                "branch": "Dubai",
                "currency": "USD",
                "exchangeRate": rates[entry_date],
                "narration": "SAMPLE - LAB DEMO ONLY; " + narration,
                "reference": source_id,
                "lines": [
                    {"accountId": account, "description": description,
                     "debitAmount": debit, "creditAmount": credit}
                    for account, debit, credit, description in source_lines
                ],
            })
            state["manual_journals"][source_id] = {"id": value["id"], "status": "Draft"}
            save(state)
        if value["status"] != "Posted":
            value = request(7017, f"/api/journal-entries/{value['id']}/post?companyId=1", {
                "rowVersion": value["rowVersion"],
                "reason": "Imported fictional source journal " + source_id,
            })
        state["manual_journals"][source_id] = {"id": value["id"], "status": "Posted"}
        save(state)
        print("Posted manual journal", source_id, value["id"])
        done += 1
        if limit and done >= limit:
            break


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("stage", choices=["contacts", "invoices", "expense_accounts", "bills", "receipts", "supplier_payments", "bank_adjustments", "manual_journals"])
    parser.add_argument("--limit", type=int, default=0)
    args = parser.parse_args()
    rows = workbook_rows()
    state = json.loads(STATE.read_text()) if STATE.exists() else {}
    if args.stage == "contacts":
        import_contacts(rows, state)
    elif args.stage == "invoices":
        import_invoices(rows, state, args.limit)
    elif args.stage == "expense_accounts":
        import_expense_accounts(state)
    elif args.stage == "bills":
        import_bills(rows, state, args.limit)
    elif args.stage == "receipts":
        import_receipts(rows, state, args.limit)
    elif args.stage == "supplier_payments":
        import_supplier_payments(rows, state, args.limit)
    elif args.stage == "bank_adjustments":
        import_bank_adjustments(rows, state, args.limit)
    elif args.stage == "manual_journals":
        import_manual_journals(rows, state, args.limit)
    print("Saved state:", STATE)


if __name__ == "__main__":
    main()
