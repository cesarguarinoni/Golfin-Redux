// REAL get_design_context dump — node 13330:1178, file 5gEAHjl6xAtW8iYY7NMvWd.
// Captured 2026-07-03 by architect (Claude Code) as the Option-B parser ground-truth fixture.
// This is what get_design_context ACTUALLY returns: React + Tailwind JSX (NOT a {elements:[]} array).
// Per-node visual attributes live in Tailwind classes + inline styles, keyed by data-node-id:
//   fills   -> bg-[#hex] / bg-gradient-to-b from-[..] to-[..] / style={{backgroundImage:"linear-gradient(...)"}} / <img src=..>
//   border  -> border / border-2 / border-[1.5px] + border-[#hex|rgba(...)]
//   radius  -> rounded-[Npx] (also rounded-tl/br-[..] per-corner)
//   text    -> <p> font-['Family:Weight'] font-{bold|semibold|normal} text-[Npx] text-[#hex|white]
// Join to menu_row_13330-1178_metadata.xml on data-node-id for exact w/h (JSX often uses size-full/relative).
const imgMenuWhiskyFlight = "https://www.figma.com/api/mcp/asset/c19169c5-79cf-4465-b8be-423a5ed800d6";
const imgFrame8 = "https://www.figma.com/api/mcp/asset/92c2d75d-1389-4b71-a75e-75f8af35b751";
const imgRpIconContainer = "https://www.figma.com/api/mcp/asset/a9dfc022-abdc-4d51-8e96-824d28c91a17";
const imgEllipse2 = "https://www.figma.com/api/mcp/asset/38f3eeef-bb4a-4c13-95de-5aa8a585cd33";

export default function MenuItemWhiskyFlight() {
  return (
    <div className="bg-gradient-to-b border-2 border-[rgba(62,124,168,0.85)] border-solid from-[#133453] overflow-clip relative rounded-[32px] shadow-[0px_4px_8px_0px_rgba(0,0,0,0.35)] size-full to-[#091b33]" data-node-id="13330:1178" data-name="Menu Item: whisky-flight">
      <div className="absolute border border-[#0a1d35] border-solid h-[152px] left-[2px] rounded-[28px] top-[2px] w-[1018px]" data-node-id="13330:1179" data-name="Frame" />
      <div className="absolute border-[1.5px] border-[rgba(62,124,168,0.7)] border-solid left-[18px] rounded-[22px] size-[124px] top-[16px]" data-node-id="13330:1180" data-name="menu_whisky-flight">
        <img alt="" className="absolute inset-0 max-w-none object-cover pointer-events-none rounded-[22px] size-full" src={imgMenuWhiskyFlight} />
      </div>
      <div className="absolute h-[130px] left-[166px] top-[16px] w-[500px]" data-node-id="13330:1181">
        <p className="[word-break:break-word] absolute font-['Noto_Sans_JP:Bold'] font-bold leading-[normal] left-0 text-[28px] text-white top-[38px] w-[248px]" data-node-id="13330:1184">
          山崎 Whisky Flight
        </p>
        <p className="[word-break:break-word] absolute font-['Rubik:Regular'] font-normal h-[24px] leading-[normal] left-0 text-[#c7d6eb] text-[18px] top-[76px] w-[500px]" data-node-id="13330:1185">
          3-glass tasting — Yamazaki, Hibiki, Hakushu
        </p>
        <div className="absolute content-stretch flex gap-[6px] items-center justify-center left-0 overflow-clip top-[104px]" data-node-id="13330:1186" data-name="Frame">
          <div className="h-[22.5px] relative shrink-0 w-[27.5px]" data-node-id="13330:1187">
            <img alt="" className="absolute inset-0 max-w-none object-contain pointer-events-none size-full" src={imgFrame8} />
          </div>
          <p className="[word-break:break-word] font-['Rubik:Bold'] font-bold leading-[normal] relative shrink-0 text-[#73e080] text-[22px] whitespace-nowrap" data-node-id="13330:1188">
            +40 STA
          </p>
        </div>
        <div className="absolute bg-[rgba(217,219,235,0.2)] border-[#d9dbeb] border-[1.5px] border-solid content-stretch flex items-center justify-center left-0 overflow-clip px-[12px] py-[4px] rounded-[14px] top-0" data-node-id="13330:1401" data-name="Tier Badge">
          <p className="[word-break:break-word] bg-clip-text bg-gradient-to-b font-['Rubik:Bold'] font-bold from-white leading-[normal] relative shrink-0 text-[16px] text-[transparent] to-[#828fa1] via-[#d1d6e0] via-[40%] whitespace-nowrap" data-node-id="13330:1402">
            MEDIUM BOOST
          </p>
        </div>
      </div>
      <div className="absolute h-[121px] left-[761px] top-[17px] w-[215px]" data-node-id="13330:1189">
        <div className="absolute bg-[#001e39] h-[56px] left-0 rounded-[43.077px] top-0 w-[215px]" data-node-id="13330:1190" data-name="RP Container">
          <div className="absolute left-[8.62px] size-[41.354px] top-[7.32px]" data-node-id="13330:1191" data-name="RP Icon Container">
            <img alt="" className="absolute inset-0 max-w-none object-cover pointer-events-none size-full" src={imgRpIconContainer} />
          </div>
          <p className="-translate-x-1/2 [word-break:break-word] absolute font-['Rubik:SemiBold'] font-semibold leading-[46.523px] left-[132.68px] text-[33.6px] text-center text-white top-[6.03px] tracking-[-0.2068px] w-[165.415px]" data-node-id="13330:1193">
            500
          </p>
        </div>
        <div className="absolute border border-[#422100] border-solid content-stretch flex h-[56px] items-center justify-center left-0 rounded-[20px] top-[65px] w-[215px]" data-node-id="13330:1194" data-name="Main Buttons">
          <div className="border-2 border-[#ffe48b] border-solid content-stretch flex flex-[1_0_0] flex-col items-center justify-center min-w-px overflow-clip px-[24px] relative rounded-[20px]" data-node-id="13330:1195" style={{ backgroundImage: "linear-gradient(180.13804308166203deg, rgb(252, 241, 149) 0.47467%, rgb(214, 171, 66) 59.905%, rgb(187, 127, 29) 99.525%)" }} data-name="Button Container">
            <div className="absolute inset-[calc(39.18%-0.43px)_calc(19.56%-1.22px)_calc(-0.01%-2px)_calc(19.56%-1.22px)]" data-node-id="13330:1196">
              <img alt="" className="absolute block inset-0 max-w-none size-full" src={imgEllipse2} />
            </div>
            <div className="[word-break:break-word] flex flex-col font-['Rubik:SemiBold'] font-semibold justify-center leading-[0] relative shrink-0 text-[39px] text-[color:var(--primary-button,#321506)] text-center text-shadow-[0px_1px_0px_rgba(255,255,255,0.3)] tracking-[-0.24px] whitespace-nowrap" data-node-id="13330:1197">
              <p className="leading-[54px]">BUY</p>
            </div>
            <div className="absolute bg-gradient-to-b from-[rgba(255,255,255,0.4)] inset-[-2px_-2px_calc(48.33%-0.07px)_-2px] mix-blend-hard-light rounded-bl-[50px] rounded-br-[50px] rounded-tl-[20px] rounded-tr-[20px] to-[rgba(255,255,255,0.05)]" data-node-id="13330:1198" data-name="Sheen" />
          </div>
        </div>
      </div>
    </div>
  );
}
