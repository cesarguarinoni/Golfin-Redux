export default function Banner() {
  return (
    <div className="content-stretch flex flex-col items-center justify-between overflow-clip pt-[12px] relative rounded-[20px] size-full" data-node-id="14280:33549" data-name="Banner">
      <div className="absolute bg-gradient-to-r content-stretch flex from-[rgba(228,0,127,0.96)] h-[64px] items-center left-0 overflow-clip px-[28px] py-[8px] to-[rgba(255,79,166,0.96)] top-[189px] w-[882px]" data-node-id="14281:33634" data-name="Tagline/Ribbon">
        <p className="[word-break:break-word] font-['Rubik:SemiBold'] font-semibold leading-[48px] relative shrink-0 text-[40px] text-white tracking-[-0.9px] whitespace-nowrap" data-node-id="14281:33635">{`GET BogeyB Drivers & Woods`}</p>
      </div>
      <div className="absolute content-stretch flex flex-col h-[188px] items-center justify-center left-0 overflow-clip px-[41px] py-[22px] top-[1035px] w-[882px]" data-node-id="14281:33636" style={{ backgroundImage: "linear-gradient(90deg, rgba(11, 27, 58, 0) 0%, rgba(11, 27, 58, 0.93) 16%, rgba(11, 27, 58, 0.93) 84%, rgba(11, 27, 58, 0) 100%)" }} data-name="Tagline/Hook">
        <div className="[word-break:break-word] font-['Rubik:SemiBold'] font-semibold leading-[0] relative shrink-0 text-[62px] text-center text-white tracking-[-1.4px] w-[800px]" data-node-id="14281:33637">
          <p className="leading-[72px] mb-0">3× RATE-UP ON</p>
          <p>
            <span className="leading-[72px] text-[#ff2d9b]">LEGENDARY</span>
            <span className="leading-[72px]">{` GEAR!`}</span>
          </p>
        </div>
      </div>
    </div>
  );
}
