from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph
from reportlab.lib import colors
from PIL import Image as PILImage

ROOT = Path(r"G:\Unity Projects\DineIn\DineIn")
OUT = ROOT / "output" / "pdf" / "Mga_ipapacheck_kay_sir_jiro_Sept_27_2026.pdf"
IMAGES = Path(r"C:\Users\User\AppData\Local\Temp")
W, H = letter
M = 48
c = canvas.Canvas(str(OUT), pagesize=letter)
c.setTitle("Mga ipapacheck kay sir Jiro Sept 27 2026")
c.setAuthor("Dine In Team")

body = ParagraphStyle("body", fontName="Helvetica", fontSize=9.3, leading=13.2, textColor=colors.black)
bullet = ParagraphStyle("bullet", parent=body, leftIndent=13, firstLineIndent=-10)
caption = ParagraphStyle("caption", fontName="Helvetica", fontSize=7.7, leading=10)

def draw_paragraph(text, x, top, width, style=body):
    p = Paragraph(text, style)
    _, height = p.wrap(width, 1000)
    p.drawOn(c, x, top - height)
    return top - height

def section(text, y):
    c.setFont("Helvetica-Bold", 11.5)
    c.drawString(M, y, text)
    return y - 13

def line(text, y):
    return draw_paragraph("&#8226; " + text, M, y, W - 2*M, bullet) - 5

def footer(page):
    c.setFont("Helvetica", 7.8)
    c.setFillColor(colors.grey)
    c.drawCentredString(W/2, 25, f"Dine In progress check  |  September 27, 2026  |  {page} of 2")
    c.setFillColor(colors.black)

c.setFont("Helvetica-Bold", 16)
c.drawCentredString(W/2, H-47, "Mga ipapacheck kay sir Jiro")
c.setFont("Helvetica", 10)
c.drawCentredString(W/2, H-65, "Dine In - progress since September 12, 2026")
y = H-90
y = draw_paragraph("Since our last progress checking, we improved Casual Dining and its tutorial and multiplayer flow, then built out the Fast Food restaurant and started its cooking gameplay.", M, y, W-2*M) - 14

y = section("September 13-19", y)
y = line("We added offline-first cloud campaign saving and connected the menu wallet to our local campaign money.", y)
y = line("We improved the Management Computer UI for PC and mobile and polished the Casual Dining tutorial.", y)
y = line("We fixed multiplayer service flow, task tracking, day-start synchronization, and shared UI.", y)
y = line("We adjusted customer arrival, stockout patience, and hygiene pacing, and started Fast Food development.", y)

y -= 8
y = section("September 20-26", y)
y = line("We built out the Lobby2 Fast Food takeout and service flow and strengthened save recovery.", y)
y = line("We polished restaurant progression, customer flow, HUD, takeout handling, and kitchen visuals.", y)
y = line("We fixed Big Boss tutorial input locks and adjusted Fast Food staff and trolley positions.", y)
y = line("We started hands-on cooking gameplay and continued work on the restaurant, staff models, and face assets.", y)

y -= 8
y = section("Mga ipapacheck kay Sir Jiro", y)
y = line("Casual Dining gameplay and tutorial progression.", y)
y = line("Host and Guest multiplayer service, payment, and shared progress.", y)
y = line("Fast Food customer flow, kitchen cooking, and restaurant progression.", y)
y = line("Local/cloud save and money synchronization.", y)

footer(1)
c.showPage()

c.setFont("Helvetica-Bold", 13)
c.drawString(M, H-45, "Development screenshots")
c.setFont("Helvetica", 8.7)
c.drawString(M, H-61, "Screenshots from our development work since the previous checking.")

def figure(name, label, x, top, boxw, boxh):
    path = IMAGES / name
    with PILImage.open(path) as im:
        iw, ih = im.size
    s = min(boxw/iw, boxh/ih)
    dw, dh = iw*s, ih*s
    c.drawImage(str(path), x+(boxw-dw)/2, top-dh, dw, dh, preserveAspectRatio=True)
    draw_paragraph(label, x, top-dh-4, boxw, caption)

figure("codex-clipboard-cb01630c-54d1-4982-812b-953a069046f6.png",
       "1. Recent project history", 48, 715, 184, 294)
figure("codex-clipboard-441836bb-5373-49ce-b471-c792fd27fe77.png",
       "2. Fast Food restaurant in Unity", 250, 715, 314, 137)
figure("codex-clipboard-b5d77797-3d4b-4ce9-acfe-4a48e84ff532.png",
       "3. Casual Dining gameplay", 250, 535, 314, 169)
figure("codex-clipboard-15b49f7f-2243-481e-96ce-3a883c91a83d.png",
       "4. Staff model variants", 48, 334, 247, 161)
figure("codex-clipboard-7f1bc9b4-392c-457d-b11e-786d50d4019d.png",
       "5. Character face assets", 317, 334, 247, 161)

footer(2)
c.save()
print(OUT)
