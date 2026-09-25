from pathlib import Path
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Image, KeepTogether, PageBreak
from PIL import Image as PILImage

ROOT = Path(r"G:\Unity Projects\DineIn\DineIn")
OUT = ROOT / "output" / "pdf" / "Mga_ipapacheck_kay_sir_jiro_Sept_27_2026.pdf"
OUT.parent.mkdir(parents=True, exist_ok=True)
SHOTS = Path(r"C:\Users\User\AppData\Local\Temp")

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name="DocTitle", parent=styles["Normal"], fontName="Helvetica-Bold", fontSize=17, leading=21, alignment=TA_CENTER, spaceAfter=7))
styles.add(ParagraphStyle(name="SubTitle", parent=styles["Normal"], fontName="Helvetica", fontSize=11, leading=14, alignment=TA_CENTER, spaceAfter=18))
styles.add(ParagraphStyle(name="Section", parent=styles["Normal"], fontName="Helvetica-Bold", fontSize=12, leading=15, spaceBefore=14, spaceAfter=7))
styles.add(ParagraphStyle(name="BodyX", parent=styles["Normal"], fontName="Helvetica", fontSize=9.5, leading=13.5, spaceAfter=6))
styles.add(ParagraphStyle(name="BulletX", parent=styles["BodyX"], leftIndent=16, firstLineIndent=-10, spaceAfter=5))
styles.add(ParagraphStyle(name="CaptionX", parent=styles["Normal"], fontName="Helvetica", fontSize=8.5, leading=11.5, spaceBefore=5, spaceAfter=15))

story = []
def p(s, style="BodyX"):
    story.append(Paragraph(s, styles[style]))
def bullet(s):
    p("&#8226; " + s, "BulletX")
def section(s):
    p(s, "Section")

p("Mga ipapacheck kay sir Jiro", "DocTitle")
p("Dine In - Progress Update | September 27, 2026", "SubTitle")
p("<b>Coverage:</b> Changes made after the September 12 progress checking, through September 26, 2026. This is a progress summary for review, not a claim that every feature has passed final testing.")

section("Week 1 - September 13 to 19")
p("<b>BIGGEST CHANGE:</b> Campaign data, tutorial flow, and Casual Dining multiplayer were strengthened while Fast Food development began.")
bullet("Added offline-first cloud campaign syncing and connected the menu wallet to locally saved campaign money (September 13).")
bullet("Improved the Management Computer UI for clearer use on PC and mobile (September 14).")
bullet("Polished and stabilized the Lobby1 tutorial presentation and progression (September 15).")
bullet("Repaired Casual Dining multiplayer service flow, day-start synchronization, task tracking, and shared service UI (September 16).")
bullet("Improved stockout patience and customer arrival pacing; added editable hygiene settings and adjusted dirt pacing (September 17-18).")
bullet("Started the Fast Food restaurant and gameplay work (September 18-19).")

section("Week 2 - September 20 to 26")
p("<b>BIGGEST CHANGE:</b> Fast Food expanded into a service loop, followed by progression, customer-flow, kitchen, and visual polish.")
bullet("Built out the Lobby2 takeout/service flow and strengthened campaign save recovery (September 20).")
bullet("Fixed restaurant flow, customer service, HUD persistence, and bubble presentation (September 21).")
bullet("Added Fast Food progression and polished its customer flow, restaurant UI, and takeout handling (September 22).")
bullet("Made Big Boss unlock tutorials optional to avoid input locks; corrected authored staff and trolley positions in Fast Food (September 24).")
bullet("Started hands-on cooking gameplay and continued kitchen polish (September 25).")
bullet("Continued visual development of the Fast Food environment, restaurant scene, staff models, and character-face assets, as shown in the screenshots below.")

section("Main items to check with Sir Jiro")
bullet("Casual Dining: demonstrate one complete day, including customer service, payment, end-of-day results, and restored progress.")
bullet("Tutorial: confirm a new player can finish the guided sequence and return to the campaign without getting stuck.")
bullet("Multiplayer: test Host and Guest together through order, service, payment, shared money, end day, and reconnect.")
bullet("Fast Food: review the current takeout flow, kitchen cooking/assembly, customer handoff, and progression.")
bullet("Saving and money: check local reload, PlayFab cloud sync on another device, and Host/Guest balance consistency.")

story.append(KeepTogether([Paragraph("Current development stage", styles["Section"]), Paragraph("The principal Casual Dining, tutorial, multiplayer, and Fast Food systems have implementation paths. The adviser demonstration should focus on real playthroughs and synchronization checks; full end-to-end playability and bug-free behavior are not yet verified by this document.", styles["BodyX"])]))

section("Development screenshots")
p("Selected proof of code history and ongoing visual/gameplay work.")

figures = [
    ("codex-clipboard-cb01630c-54d1-4982-812b-953a069046f6.png", "Figure 1. Recent repository history, including Fast Food, tutorial, multiplayer, and kitchen-polish commits.", 5.2, 4.1),
    ("codex-clipboard-441836bb-5373-49ce-b471-c792fd27fe77.png", "Figure 2. Fast Food restaurant environment prefab shown in the Unity Editor.", 6.4, 3.4),
    ("codex-clipboard-b5d77797-3d4b-4ce9-acfe-4a48e84ff532.png", "Figure 3. Casual Dining gameplay view showing customer/staff presentation and the current HUD.", 6.4, 3.7),
    ("codex-clipboard-15b49f7f-2243-481e-96ce-3a883c91a83d.png", "Figure 4. Staff character model variants under development in Blender.", 6.4, 3.5),
    ("codex-clipboard-7f1bc9b4-392c-457d-b11e-786d50d4019d.png", "Figure 5. Character facial-expression texture assets in development.", 6.4, 3.5),
]

for idx, (filename, caption, maxw, maxh) in enumerate(figures):
    path = SHOTS / filename
    with PILImage.open(path) as im:
        w, h = im.size
    scale = min(maxw * inch / w, maxh * inch / h)
    image = Image(str(path), width=w * scale, height=h * scale)
    image.hAlign = "CENTER"
    story.append(KeepTogether([image, Paragraph(caption, styles["CaptionX"])]))

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.grey)
    canvas.drawCentredString(letter[0]/2, 0.38*inch, f"Dine In progress check - September 27, 2026  |  Page {doc.page}")
    canvas.restoreState()

doc = SimpleDocTemplate(str(OUT), pagesize=letter, leftMargin=.75*inch, rightMargin=.75*inch,
                        topMargin=.63*inch, bottomMargin=.6*inch,
                        title="Mga ipapacheck kay sir Jiro Sept 27 2026", author="Dine In Team")
doc.build(story, onFirstPage=footer, onLaterPages=footer)
print(OUT)
