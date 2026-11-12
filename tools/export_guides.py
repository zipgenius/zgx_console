"""Export the HTML manuals to Wiki Markdown and print-ready PDFs.

Requires beautifulsoup4, markdownify and playwright (with Chromium installed).
"""
from pathlib import Path
import asyncio
from bs4 import BeautifulSoup
from markdownify import markdownify
from playwright.async_api import async_playwright

ROOT = Path(__file__).resolve().parents[1]
PAGES = {'it': 'Guida-completa', 'en': 'Command-reference'}
PRINT_CSS = '''
@page { size:A4; margin:16mm 14mm 18mm; }
:root { color-scheme:light; }
body { background:white; color:#172033; font-size:10pt; }
header { padding:16px 20px; print-color-adjust:exact; -webkit-print-color-adjust:exact; }
.brand img { width:76px; height:76px; }
.brand h1 { font-size:34pt; }
.layout { display:block; padding:16px 0 0; }
nav { display:none; }
main > section { border:0; border-radius:0; padding:0; margin-bottom:18px; }
h2, h3 { break-after:avoid; }
h2 { font-size:17pt; }
h3 { font-size:12pt; }
p, li { orphans:3; widows:3; }
table { display:table; font-size:9pt; overflow:visible; }
thead { display:table-header-group; }
tr, pre, .note { break-inside:avoid; }
th,td { padding:6px; overflow-wrap:anywhere; }
pre { white-space:pre-wrap; overflow-wrap:anywhere; font-size:8.5pt; }
footer { padding:0; font-size:9pt; }
'''

async def main():
    out = ROOT / 'output/pdf'
    out.mkdir(parents=True, exist_ok=True)
    async with async_playwright() as pw:
        browser = await pw.chromium.launch()
        for lang, name in PAGES.items():
            source = ROOT / f'zgx/help/{lang}/index.html'
            soup = BeautifulSoup(source.read_text(encoding='utf-8'), 'html.parser')
            main = soup.find('main')
            # Preserve explicit HTML IDs: translated titles do not always match them.
            for section in main.find_all('section', id=True):
                section.insert_before(f'WIKI_ANCHOR_{section["id"]}_END')
            body = markdownify(str(main), heading_style='ATX', bullets='-')
            import re
            body = re.sub(r'WIKI\\?_ANCHOR\\?_(.*?)\\?_END',
                          lambda m: '<a name="' + m[1].replace('\\', '') + '"></a>', body)
            intro = ('Versione 1.0 - 12 novembre 2026' if lang == 'it'
                     else 'Version 1.0 - 12 November 2026')
            download = f'https://github.com/zipgenius/zgx_console/wiki/downloads/ZipGenius-X-console-{lang}.pdf'
            wiki = f'# ZipGenius X console\n\n{intro}\n\n[Italiano](Guida-completa) | [English](Command-reference) | [PDF]({download})\n\n' + body
            (ROOT / f'docs/wiki/{name}.md').write_text(wiki, encoding='utf-8')
            page = await browser.new_page()
            await page.goto(source.as_uri())
            await page.emulate_media(media='print', color_scheme='light')
            await page.add_style_tag(content=PRINT_CSS)
            await page.evaluate('document.fonts.ready')
            assert await page.locator('.brand img').evaluate('(img) => img.complete && img.naturalWidth > 0')
            await page.pdf(path=str(out / f'ZipGenius-X-console-{lang}.pdf'),
                           print_background=True, display_header_footer=True,
                           header_template='<span></span>',
                           footer_template='<div style="width:100%;text-align:center;font:9px Arial;color:#667085">ZipGenius X console · <span class="pageNumber"></span> / <span class="totalPages"></span></div>',
                           prefer_css_page_size=True)
            await page.close()
        await browser.close()

if __name__ == '__main__':
    asyncio.run(main())
