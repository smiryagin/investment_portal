const holdings = [
  { symbol: "VTI", name: "Vanguard Total Stock Market", value: "$64,250", allocation: "32.4%", return: "+12.8%", positive: true },
  { symbol: "VXUS", name: "Vanguard Total International", value: "$34,860", allocation: "17.6%", return: "+8.4%", positive: true },
  { symbol: "BND", name: "Vanguard Total Bond Market", value: "$29,740", allocation: "15.0%", return: "+2.1%", positive: true },
  { symbol: "MSFT", name: "Microsoft Corporation", value: "$21,420", allocation: "10.8%", return: "+18.6%", positive: true },
  { symbol: "CASH", name: "Cash reserve", value: "$18,900", allocation: "9.5%", return: "0.0%", positive: false },
];

const activity = [
  { date: "Aug 8", label: "Dividend received", detail: "VTI quarterly distribution", amount: "+$286.42" },
  { date: "Aug 5", label: "Automatic investment", detail: "Monthly brokerage contribution", amount: "+$1,500.00" },
  { date: "Jul 31", label: "Rebalanced portfolio", detail: "Moved 1.8% into international equity", amount: "$3,420.00" },
];

export default function Home() {
  return (
    <main>
      <header className="topbar">
        <a className="brand" href="#top" aria-label="Northstar home">
          <span className="brand-mark">N</span>
          <span>Northstar</span>
        </a>
        <nav aria-label="Main navigation">
          <a className="active" href="#overview">Overview</a>
          <a href="#holdings">Holdings</a>
          <a href="#goals">Goals</a>
        </nav>
        <div className="profile" aria-label="Signed in as Andrey">
          <span className="avatar">AS</span>
          <span className="profile-copy"><strong>Andrey</strong><small>Personal account</small></span>
        </div>
      </header>

      <div className="shell" id="top">
        <section className="hero" id="overview">
          <div>
            <p className="eyebrow">Portfolio overview</p>
            <h1>Good afternoon, Andrey.</h1>
            <p className="lede">Your long-term plan is on track. Here is how your portfolio is moving today.</p>
          </div>
          <div className="as-of"><span className="live-dot" /> Sample data Â· Aug 10, 2026</div>
        </section>

        <section className="summary-grid" aria-label="Portfolio summary">
          <article className="balance-card">
            <div className="card-heading"><span>Total portfolio value</span><span className="pill">All accounts</span></div>
            <div className="balance-row">
              <div><strong className="balance">$198,420.67</strong><p className="gain"><span>â†‘ $1,284.20</span> today Â· 0.65%</p></div>
              <div className="range" aria-label="Chart range"><span>1D</span><span>1W</span><span>1M</span><span>1Y</span><span className="selected">All</span></div>
            </div>
            <div className="chart" role="img" aria-label="Portfolio value grew steadily over the selected period">
              <div className="chart-fill" />
              <div className="chart-line"><i /><i /><i /><i /><i /><i /><i /><i /><i /></div>
              <span className="chart-label start">2021</span><span className="chart-label end">Today</span>
            </div>
          </article>

          <article className="allocation-card">
            <div className="card-heading"><span>Asset allocation</span><button type="button">View details</button></div>
            <div className="allocation-content">
              <div className="donut" aria-label="65 percent stocks, 20 percent bonds, 10 percent cash, 5 percent alternatives"><div><strong>65%</strong><span>Stocks</span></div></div>
              <ul className="legend">
                <li><i className="dot stocks" /><span>Stocks</span><strong>65%</strong></li>
                <li><i className="dot bonds" /><span>Bonds</span><strong>20%</strong></li>
                <li><i className="dot cash" /><span>Cash</span><strong>10%</strong></li>
                <li><i className="dot alt" /><span>Alternatives</span><strong>5%</strong></li>
              </ul>
            </div>
            <p className="allocation-note">Your allocation is within 2% of its target.</p>
          </article>
        </section>

        <section className="content-grid">
          <article className="panel holdings" id="holdings">
            <div className="section-heading"><div><p className="eyebrow">Portfolio</p><h2>Top holdings</h2></div><button type="button">See all holdings â†’</button></div>
            <div className="table-wrap">
              <table>
                <thead><tr><th>Investment</th><th>Market value</th><th>Allocation</th><th>Total return</th></tr></thead>
                <tbody>{holdings.map((holding) => (
                  <tr key={holding.symbol}>
                    <td><span className="ticker">{holding.symbol}</span><span className="holding-name">{holding.name}</span></td>
                    <td>{holding.value}</td><td>{holding.allocation}</td><td className={holding.positive ? "positive" : "muted"}>{holding.return}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          </article>

          <aside className="side-stack">
            <article className="panel goal" id="goals">
              <div className="section-heading"><div><p className="eyebrow">Primary goal</p><h2>Financial freedom</h2></div><span className="goal-icon">2038</span></div>
              <div className="goal-number"><strong>$198,421</strong><span>of $1,000,000</span></div>
              <div className="progress" aria-label="20 percent of goal completed"><span /></div>
              <div className="goal-meta"><span>20% funded</span><span>12 years remaining</span></div>
              <p className="goal-status">On track</p>
            </article>

            <article className="panel insight">
              <p className="eyebrow">Northstar insight</p>
              <h2>Your cash is ready to work.</h2>
              <p>You are holding 1.5% more cash than your target. Investing the difference could keep your plan aligned.</p>
              <button type="button">Review opportunity</button>
            </article>
          </aside>
        </section>

        <section className="panel activity">
          <div className="section-heading"><div><p className="eyebrow">Accounts</p><h2>Recent activity</h2></div><button type="button">View all activity â†’</button></div>
          <div className="activity-list">{activity.map((item) => (
            <div className="activity-row" key={item.date + item.label}>
              <span className="activity-date">{item.date}</span><span className="activity-symbol">â†—</span>
              <span><strong>{item.label}</strong><small>{item.detail}</small></span><strong className="activity-amount">{item.amount}</strong>
            </div>
          ))}</div>
        </section>

        <footer><span>Northstar Investment Portal</span><span>Sample data for demonstration only Â· Not investment advice</span></footer>
      </div>
    </main>
  );
}
