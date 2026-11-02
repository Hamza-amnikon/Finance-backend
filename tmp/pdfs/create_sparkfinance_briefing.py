from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "pdf" / "SparkFinance_Project_Presentation_Brief.pdf"
OUTPUT.parent.mkdir(parents=True, exist_ok=True)

PAGE_WIDTH, PAGE_HEIGHT = A4
NAVY = colors.HexColor("#102A56")
BLUE = colors.HexColor("#2463EB")
SKY = colors.HexColor("#EAF2FF")
PALE_BLUE = colors.HexColor("#F5F8FE")
GREEN = colors.HexColor("#0E8A55")
PALE_GREEN = colors.HexColor("#EAF8F1")
AMBER = colors.HexColor("#A35C00")
PALE_AMBER = colors.HexColor("#FFF5E5")
RED = colors.HexColor("#B42318")
PALE_RED = colors.HexColor("#FFF0EE")
TEXT = colors.HexColor("#17223B")
MUTED = colors.HexColor("#5B6B86")
BORDER = colors.HexColor("#D8E1F0")
WHITE = colors.white


styles = getSampleStyleSheet()
styles.add(ParagraphStyle(
    name="CoverTitle",
    parent=styles["Title"],
    fontName="Helvetica-Bold",
    fontSize=28,
    leading=34,
    textColor=NAVY,
    alignment=TA_LEFT,
    spaceAfter=12,
))
styles.add(ParagraphStyle(
    name="CoverSubtitle",
    parent=styles["Normal"],
    fontName="Helvetica",
    fontSize=14,
    leading=20,
    textColor=MUTED,
    spaceAfter=10,
))
styles.add(ParagraphStyle(
    name="SectionTitle",
    parent=styles["Heading1"],
    fontName="Helvetica-Bold",
    fontSize=20,
    leading=25,
    textColor=NAVY,
    spaceBefore=2,
    spaceAfter=12,
))
styles.add(ParagraphStyle(
    name="SubTitle",
    parent=styles["Heading2"],
    fontName="Helvetica-Bold",
    fontSize=13,
    leading=17,
    textColor=NAVY,
    spaceBefore=8,
    spaceAfter=5,
))
styles.add(ParagraphStyle(
    name="BodySmall",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=9.2,
    leading=13.2,
    textColor=TEXT,
    spaceAfter=5,
))
styles.add(ParagraphStyle(
    name="Body",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=10.2,
    leading=14.6,
    textColor=TEXT,
    spaceAfter=7,
))
styles.add(ParagraphStyle(
    name="BriefBullet",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=9.7,
    leading=13.5,
    leftIndent=12,
    firstLineIndent=-8,
    textColor=TEXT,
    spaceAfter=3,
))
styles.add(ParagraphStyle(
    name="Quote",
    parent=styles["BodyText"],
    fontName="Helvetica-Oblique",
    fontSize=11,
    leading=16,
    leftIndent=12,
    rightIndent=12,
    textColor=NAVY,
    spaceAfter=0,
))
styles.add(ParagraphStyle(
    name="TableHead",
    parent=styles["BodyText"],
    fontName="Helvetica-Bold",
    fontSize=8.5,
    leading=11,
    textColor=WHITE,
))
styles.add(ParagraphStyle(
    name="TableCell",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=8.2,
    leading=10.8,
    textColor=TEXT,
))
styles.add(ParagraphStyle(
    name="SmallMuted",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=8.5,
    leading=11.5,
    textColor=MUTED,
))
styles.add(ParagraphStyle(
    name="CenterSmall",
    parent=styles["BodyText"],
    fontName="Helvetica-Bold",
    fontSize=9,
    leading=12,
    textColor=NAVY,
    alignment=TA_CENTER,
))


def P(text, style="Body"):
    return Paragraph(text, styles[style])


def bullet(text):
    return P("- " + text, "BriefBullet")


def callout(title, body, background=SKY, accent=BLUE):
    data = [[
        P(title, "SubTitle"),
        P(body, "BodySmall"),
    ]]
    table = Table(data, colWidths=[42 * mm, 125 * mm])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), background),
        ("BOX", (0, 0), (-1, -1), 0.7, accent),
        ("LINEBEFORE", (0, 0), (0, -1), 4, accent),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
    ]))
    return table


def info_cards(cards):
    cells = []
    for value, label in cards:
        cells.append(P(f"<font size='16'><b>{value}</b></font><br/>{label}", "CenterSmall"))
    table = Table([cells], colWidths=[167 * mm / len(cells)] * len(cells))
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), PALE_BLUE),
        ("BOX", (0, 0), (-1, -1), 0.7, BORDER),
        ("INNERGRID", (0, 0), (-1, -1), 0.5, BORDER),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 10),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
    ]))
    return table


def styled_table(headers, rows, widths, font_size=8.2):
    data = [[P(h, "TableHead") for h in headers]]
    for row in rows:
        data.append([P(str(cell), "TableCell") for cell in row])
    table = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), NAVY),
        ("TEXTCOLOR", (0, 0), (-1, 0), WHITE),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [WHITE, PALE_BLUE]),
        ("GRID", (0, 0), (-1, -1), 0.5, BORDER),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]))
    return table


class BriefingDoc(BaseDocTemplate):
    def __init__(self, filename):
        super().__init__(
            filename,
            pagesize=A4,
            leftMargin=21 * mm,
            rightMargin=21 * mm,
            topMargin=19 * mm,
            bottomMargin=18 * mm,
            title="SparkFinance Project Presentation Brief",
            author="SparkFinance Project Team",
            subject="CA, Manager and Senior Manager briefing",
        )
        frame = Frame(
            self.leftMargin,
            self.bottomMargin,
            self.width,
            self.height,
            id="main",
        )
        self.addPageTemplates(PageTemplate(
            id="content",
            frames=[frame],
            onPage=draw_page,
        ))


def draw_page(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(NAVY)
    canvas.rect(0, PAGE_HEIGHT - 9 * mm, PAGE_WIDTH, 9 * mm, fill=1, stroke=0)
    canvas.setFont("Helvetica-Bold", 8)
    canvas.setFillColor(WHITE)
    canvas.drawString(21 * mm, PAGE_HEIGHT - 6 * mm, "SPARKFINANCE")

    canvas.setStrokeColor(BORDER)
    canvas.line(21 * mm, 13 * mm, PAGE_WIDTH - 21 * mm, 13 * mm)
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(MUTED)
    canvas.drawString(21 * mm, 8.5 * mm, "Project presentation brief - 2 November 2026")
    canvas.drawRightString(PAGE_WIDTH - 21 * mm, 8.5 * mm, f"Page {doc.page}")
    canvas.restoreState()


story = []

# Cover
story.extend([
    Spacer(1, 21 * mm),
    P("SparkFinance", "CoverTitle"),
    P("Project Presentation Brief", "CoverTitle"),
    P("Prepared for CA, Manager and Senior Manager review", "CoverSubtitle"),
    Spacer(1, 9 * mm),
    callout(
        "Purpose",
        "A practical guide to demonstrate the system, explain each module, answer cross-questions and present the current product status honestly.",
        PALE_BLUE,
        BLUE,
    ),
    Spacer(1, 11 * mm),
    info_cards([
        ("React", "Frontend"),
        (".NET 10", "Backend"),
        ("SQL Server", "Database"),
        ("AE + IN", "Country scope"),
    ]),
    Spacer(1, 13 * mm),
    P("Core presentation message", "SubTitle"),
    callout(
        "Say this",
        "SparkFinance already manages the operational finance lifecycle. The Chart of Accounts provides the UAE and India accounting foundation. The next phase connects every transaction to double-entry accounting and statutory reporting.",
        PALE_GREEN,
        GREEN,
    ),
    Spacer(1, 15 * mm),
    P("Presentation date: 2 November 2026", "SmallMuted"),
    P("Document type: Internal product briefing", "SmallMuted"),
    PageBreak(),
])

# Executive summary
story.extend([
    P("1. Executive summary", "SectionTitle"),
    P("Opening statement", "SubTitle"),
    callout(
        "60-second introduction",
        "SparkFinance is a modular finance-management platform designed for companies operating in the UAE and India. It covers customer sales, vendor purchases, expenses, banking, exchange rates and the Chart of Accounts. The frontend uses React, the backend uses ASP.NET Core services, and SQL Server stores the financial data. The current phase focuses on controlled business-document workflows. The next phase completes automated double-entry posting, ledger reports, authentication and production deployment.",
    ),
    Spacer(1, 8),
    P("Verified build status", "SubTitle"),
    styled_table(
        ["Area", "Result", "Presentation meaning"],
        [
            ["Frontend", "Build successful", "The React application compiles and is demo-ready."],
            ["Original backend solution", "12 services build, 0 errors", "Core service projects compile successfully."],
            ["Accounting service", "Builds separately, 0 errors", "Chart of Accounts works, but start it separately."],
            ["Warnings", "Package and bundle warnings", "Do not call the solution production-ready yet."],
            ["Tests", "Some logic checks pass; SQL harnesses blocked", "Broader automated test and CI work is still required."],
        ],
        [36 * mm, 38 * mm, 93 * mm],
    ),
    Spacer(1, 8),
    callout(
        "Honest positioning",
        "Present this as a strong working finance prototype and accounting foundation. Do not claim that authentication, complete general-ledger posting, statutory reporting or production deployment are finished.",
        PALE_AMBER,
        AMBER,
    ),
    P("Architecture explanation", "SubTitle"),
    bullet("React and Vite provide the user interface and route requests to the relevant service."),
    bullet("ASP.NET Core services separate sales, purchases, expenses, banking, exchange rates, dashboard and accounting responsibilities."),
    bullet("The services currently share one SQL Server database. Describe this as modular services, not fully isolated microservices."),
    bullet("Database validation, unique constraints, row-version concurrency and protected system accounts support financial control."),
    PageBreak(),
])

# Demo plan
story.extend([
    P("2. Recommended demonstration plan", "SectionTitle"),
    P("Keep the presentation business-first. Show the accounting structure after the operational workflows so the audience understands what will eventually receive the postings.", "Body"),
    styled_table(
        ["Order", "Module", "Time", "What to demonstrate"],
        [
            ["1", "Dashboard", "2 min", "Management totals, branch, date and reporting-currency filters, recent activity."],
            ["2", "Sales", "4 min", "Client to estimate to invoice to payment, with credit notes for corrections."],
            ["3", "Purchases", "4 min", "Vendor to purchase order to bill to payment, with vendor credits."],
            ["4", "Expenses", "2 min", "Direct expense recording, categories, currencies and receipts."],
            ["5", "Banking", "4 min", "Accounts, transactions, transfers and reconciliation controls."],
            ["6", "Exchange rates", "2 min", "Daily read-only monitoring, source, rate date and status."],
            ["7", "Chart of Accounts", "5 min", "AE and India templates, hierarchy, posting/group accounts and protection."],
            ["8", "Roadmap", "2 min", "Journal engine, ledger, trial balance, reports, authentication and deployment."],
        ],
        [12 * mm, 32 * mm, 18 * mm, 105 * mm],
    ),
    Spacer(1, 8),
    P("Suggested transition", "SubTitle"),
    callout(
        "Narration",
        "I will first show how business documents move through the system, then how payments and banking are controlled, and finally how the new Chart of Accounts provides the accounting structure for UAE and India entities.",
        PALE_GREEN,
        GREEN,
    ),
    P("Demo principle", "SubTitle"),
    bullet("Use existing records and avoid creating too many records live."),
    bullet("Show one complete sales flow and one complete purchase flow."),
    bullet("Keep Swagger open as technical evidence, but explain the business screen first."),
    bullet("If a preview module is opened, label it clearly as the next implementation phase."),
    PageBreak(),
])

# Sales and purchases
story.extend([
    P("3. Sales and purchase lifecycle", "SectionTitle"),
    P("Sales modules", "SubTitle"),
    styled_table(
        ["Module", "How to explain it"],
        [
            ["Clients", "Customer master data: identity, contacts, address and active status. Sales documents refer to this master."],
            ["Estimates", "Customer quotation workflow. Draft estimates can be sent, accepted, declined and converted into invoices."],
            ["Invoices", "Customer sales document. It can be previewed, created and issued, after which collections can be tracked."],
            ["Payments Received", "Records money received from customers and links the collection with the related invoice."],
            ["Credit Notes", "Reduces an issued invoice for returns, overbilling or commercial corrections."],
        ],
        [36 * mm, 131 * mm],
    ),
    Spacer(1, 7),
    callout("Sales flow", "Client -> Estimate -> Accepted -> Invoice -> Payment Received. Use a Credit Note when a reduction is required.", PALE_BLUE, BLUE),
    P("Purchase modules", "SubTitle"),
    styled_table(
        ["Module", "How to explain it"],
        [
            ["Vendors", "Supplier master data including location, currencies and active status."],
            ["Purchase Orders", "Records the intention to purchase. Supports preview, issue and cancellation."],
            ["Bills", "Records the supplier invoice and payable. A bill can be created from a purchase order."],
            ["Payments Made", "Records settlement of vendor bills and shows outstanding bill balances."],
            ["Vendor Credits", "Reduces supplier liability and can be applied against eligible bills."],
        ],
        [36 * mm, 131 * mm],
    ),
    Spacer(1, 7),
    callout("Purchase flow", "Vendor -> Purchase Order -> Bill -> Payment Made. Use a Vendor Credit when the supplier reduces the amount due.", PALE_GREEN, GREEN),
    PageBreak(),
])

# Expense banking dashboard
story.extend([
    P("4. Expenses, banking and management visibility", "SectionTitle"),
    P("Expenses", "SubTitle"),
    P("The expense module records operating costs that may not use the normal purchase-order flow. It supports categories, currencies, receipts and voiding. Typical categories include travel, utilities, office costs, software and meals.", "Body"),
    P("Banking", "SubTitle"),
    styled_table(
        ["Module", "Control provided"],
        [
            ["Bank Accounts", "Maintains bank and cash accounts, currency and opening balance."],
            ["Bank Transactions", "Records money in and money out with draft, posted and cancelled states."],
            ["Bank Transfers", "Controls transfers between accounts, including sent, received, cancelled and exchange-rate handling."],
            ["Bank Reconciliation", "Compares book transactions with statements, supports exact matching and prevents duplicate consumption."],
        ],
        [40 * mm, 127 * mm],
    ),
    P("Dashboard", "SubTitle"),
    bullet("Shows revenue, expenses, payments received, payments made, receivables and payables."),
    bullet("Supports branch, date and reporting-currency filters."),
    bullet("Shows recent financial activity for management review."),
    bullet("Original transaction values remain unchanged; reporting views use reference exchange rates."),
    Spacer(1, 8),
    callout(
        "CA focus",
        "Emphasize bank reconciliation, outstanding receivables and payables, document status control, credit adjustments and the difference between original transaction currency and reporting currency.",
        PALE_AMBER,
        AMBER,
    ),
    PageBreak(),
])

# Exchange and COA
story.extend([
    P("5. Exchange rates and Chart of Accounts", "SectionTitle"),
    P("Daily exchange-rate checker", "SubTitle"),
    bullet("Read-only reference rates are retrieved from Frankfurter and stored with source and rate date."),
    bullet("The service performs a scheduled daily check while it is running."),
    bullet("It catches up after restart and retries failed checks."),
    bullet("INR to INR is always 1."),
    bullet("Refreshing the screen reloads saved results; it does not change rates."),
    bullet("Existing transaction rates remain unchanged."),
    callout("Correct terminology", "Call this an automatic scheduled exchange-rate checker, not an AI agent.", PALE_BLUE, BLUE),
    P("Chart of Accounts", "SubTitle"),
    P("The Chart of Accounts organizes financial records under Assets, Liabilities, Equity, Income and Expenses. The system supports company-specific structures for UAE and India.", "Body"),
    styled_table(
        ["Concept", "Meaning"],
        [
            ["Account code", "Human-readable accounting identifier used for classification, searching and reporting."],
            ["Account name", "Business description such as Cash on Hand or Sales Revenue."],
            ["Group account", "Organizes child accounts and does not receive direct postings."],
            ["Posting account", "Final account that can receive journal entries."],
            ["Control account", "Connects a subledger such as receivables, payables, inventory or tax."],
            ["System account", "Protected account required by the application and not freely editable."],
            ["SystemKey", "Stable internal identifier that remains reliable even if the visible account code changes."],
        ],
        [39 * mm, 128 * mm],
    ),
    PageBreak(),
])

# Country specific COA
story.extend([
    P("6. UAE and India accounting structure", "SectionTitle"),
    info_cards([
        ("37", "UAE standard accounts"),
        ("43", "India standard accounts"),
        ("5", "Main account classes"),
    ]),
    Spacer(1, 9),
    P("Example hierarchy", "SubTitle"),
    callout(
        "Account tree",
        "1000 Assets<br/>1100 Current Assets<br/>1110 Cash on Hand<br/>1120 Bank Accounts<br/>1130 Accounts Receivable",
        PALE_BLUE,
        BLUE,
    ),
    P("UAE-specific structure", "SubTitle"),
    bullet("Input VAT Recoverable under Tax Recoverable."),
    bullet("Output VAT Payable under Tax Payables."),
    P("India-specific structure", "SubTitle"),
    bullet("Input and Output CGST."),
    bullet("Input and Output SGST."),
    bullet("Input and Output IGST."),
    bullet("TDS Receivable and TDS Payable."),
    P("Controls to demonstrate", "SubTitle"),
    bullet("Every account can be viewed in both Tree and Table modes."),
    bullet("Only ordinary accounts can be edited."),
    bullet("System and control accounts are protected in both the frontend and backend."),
    bullet("The setup endpoint is repeat-safe and does not create duplicate template accounts."),
    bullet("Account codes are unique within a company."),
    PageBreak(),
])

# Readiness matrix
story.extend([
    P("7. Module readiness matrix", "SectionTitle"),
    styled_table(
        ["Area", "Status", "How to present it"],
        [
            ["Dashboard", "Live service", "Show management totals and recent activity."],
            ["Sales documents", "Live services", "Show the quote-to-cash flow."],
            ["Purchase documents", "Live services", "Show the procure-to-pay flow."],
            ["Expenses", "Live service", "Show direct expense recording and receipts."],
            ["Banking", "Live service", "Show transactions, transfers and reconciliation."],
            ["Exchange rates", "Live service", "Show read-only daily monitoring."],
            ["Chart of Accounts", "Live service", "Show AE/IN templates and protection."],
            ["Journal Entries", "Preview / roadmap", "Explain the planned posting workflow; do not claim completion."],
            ["General Ledger", "Preview / roadmap", "Requires the completed posting engine."],
            ["Trial Balance", "Preview / roadmap", "Will be calculated from posted journals."],
            ["Financial and tax reports", "Preview / roadmap", "UI exists; complete accounting backend is next."],
            ["Authentication and users", "Final phase", "Preview roles exist; complete service enforcement is pending."],
            ["Audit history and settings", "Partial / roadmap", "Present as governance work still being completed."],
        ],
        [38 * mm, 34 * mm, 95 * mm],
    ),
    Spacer(1, 8),
    callout(
        "Important",
        "Do not demonstrate preview modules as completed. Show their navigation only if the audience asks about the roadmap.",
        PALE_RED,
        RED,
    ),
    PageBreak(),
])

# CA Q&A
story.extend([
    P("8. Questions a CA may ask", "SectionTitle"),
    styled_table(
        ["Question", "Suggested answer"],
        [
            ["Why use account codes?", "Codes classify accounts and make searching and reporting consistent. 1000 is Assets, 2000 Liabilities, 3000 Equity, 4000 Income and 5000 Expenses."],
            ["Why also use SystemKey?", "The visible code may change, but SystemKey allows the application to keep identifying critical accounts reliably."],
            ["Can a group account receive money?", "No. Group accounts organize the structure. Only posting accounts receive journal entries."],
            ["Why are some accounts not editable?", "System and control accounts are protected because changing their type, balance or hierarchy could damage reporting and subledger integration."],
            ["How are UAE and India different?", "Each company receives a country-specific template. UAE uses VAT; India uses GST and TDS accounts."],
            ["Does currency conversion change the original transaction?", "No. The original amount is preserved. Reporting views use the applicable reference rate."],
            ["Is the General Ledger complete?", "The Chart of Accounts foundation is complete. Automated double-entry posting, ledger, trial balance and statements are the next accounting phase."],
            ["How will subledgers reconcile?", "Receivables, payables, inventory, tax and bank control accounts will be connected to automated postings and reconciliation reports in the next phase."],
        ],
        [59 * mm, 108 * mm],
    ),
    PageBreak(),
])

# Manager Q&A
story.extend([
    P("9. Questions managers may ask", "SectionTitle"),
    styled_table(
        ["Question", "Suggested answer"],
        [
            ["Why separate services?", "Each business area can be maintained independently. Because they currently share one database, the accurate term is modular services rather than fully isolated microservices."],
            ["Is authentication complete?", "Authentication and final role enforcement are planned for the final phase. Preview roles currently demonstrate Admin, Finance and Viewer behavior."],
            ["Is this production-ready?", "Not yet. Production readiness requires authentication, audit controls, security-package updates, broader tests, deployment automation and backups."],
            ["What prevents conflicting edits?", "The backend uses validation, unique constraints, company separation, protected system accounts and row-version concurrency."],
            ["What has been tested?", "The projects build successfully. Targeted banking matching and payment validation logic checks pass, while database-backed harnesses require the LocalDB test environment to be repaired."],
            ["What is the next major milestone?", "Connect every issued document, payment, expense and bank transaction to balanced journal entries, then deliver ledger, trial balance and statements."],
            ["Can it scale?", "The modular design supports gradual scaling, but production hosting, database strategy, monitoring and service boundaries still need formalization."],
        ],
        [59 * mm, 108 * mm],
    ),
    PageBreak(),
])

# Risks and roadmap
story.extend([
    P("10. Risks, improvements and roadmap", "SectionTitle"),
    P("Known presentation and technical risks", "SubTitle"),
    styled_table(
        ["Priority", "Item", "Required action"],
        [
            ["High", "Authentication is not complete", "Add backend identity, authorization and secure session handling."],
            ["High", "Full accounting posting is incomplete", "Build balanced journal generation and posting rules for every source module."],
            ["High", "Vulnerable package warnings", "Upgrade and retest affected cryptography dependencies."],
            ["Medium", "Accounting service missing from root solution", "Add it to SparkFinanceBackend.slnx and update startup documentation."],
            ["Medium", "Database-backed test harnesses blocked", "Repair test LocalDB setup and add repeatable CI tests."],
            ["Medium", "Shared development database", "Design production SQL hosting, migrations, backups and recovery."],
            ["Low", "Large frontend bundle", "Introduce route-level code splitting and lazy loading."],
        ],
        [20 * mm, 58 * mm, 89 * mm],
    ),
    P("Recommended delivery sequence", "SubTitle"),
    bullet("Phase 1: Journal-entry model, posting rules, fiscal periods and period locks."),
    bullet("Phase 2: Automatic postings from invoices, bills, payments, expenses and banking."),
    bullet("Phase 3: General Ledger, Trial Balance, Profit and Loss, Balance Sheet and Cash Flow."),
    bullet("Phase 4: Authentication, approval matrix, audit history and attachment governance."),
    bullet("Phase 5: Security upgrades, automated tests, deployment, monitoring and backups."),
    PageBreak(),
])

# Checklist and closing
story.extend([
    P("11. Meeting-day checklist", "SectionTitle"),
    styled_table(
        ["Check", "Action"],
        [
            ["Database", "Start SQL Server LocalDB and verify SparkFinance is accessible."],
            ["Original services", "Open SparkFinanceBackend.slnx and start the APIs required for the demo."],
            ["Accounting", "Start AccountingService.API separately because it is not yet in the root solution."],
            ["Frontend", "Start the Vite frontend and hard-refresh the browser before the meeting."],
            ["Data", "Keep existing Dubai and India records ready; avoid risky live data entry."],
            ["Screens", "Verify Dashboard, Exchange Rates, Banking and Chart of Accounts before attendees arrive."],
            ["Technical proof", "Keep Accounting Swagger available in a separate tab."],
            ["Fallback", "Keep screenshots available in case a local service or database stops."],
        ],
        [36 * mm, 131 * mm],
    ),
    Spacer(1, 10),
    P("Closing statement", "SubTitle"),
    callout(
        "End with this",
        "The current system demonstrates the complete operational finance workflow and a controlled, country-specific Chart of Accounts. The next milestone is the accounting engine that turns those operational records into balanced journals, ledgers and management and statutory reports.",
        PALE_GREEN,
        GREEN,
    ),
    Spacer(1, 12),
    P("What not to claim", "SubTitle"),
    bullet("Do not say every screen is live and production-connected."),
    bullet("Do not say authentication and security are complete."),
    bullet("Do not say all automated tests pass."),
    bullet("Do not call the exchange-rate checker an AI agent."),
    bullet("Do not call the shared-database architecture fully isolated microservices."),
    Spacer(1, 12),
    P("Prepared for internal project presentation", "SmallMuted"),
])


doc = BriefingDoc(str(OUTPUT))
doc.build(story)
print(OUTPUT)
