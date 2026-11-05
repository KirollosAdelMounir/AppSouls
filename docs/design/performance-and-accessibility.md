# Performance and accessibility acceptance criteria

The application uses a light blue design with matching dark mode. These are implementation targets and release checks, not measurements already achieved. Fast interaction must be measured under declared device, network, data volume and server load conditions; framework choice alone cannot guarantee parity with Angular.

## Performance budgets

| Measure | Initial target | Verification |
| --- | --- | --- |
| Largest Contentful Paint | At most 2.5 seconds at the 75th percentile | Production field data; use a recorded mobile lab profile before release |
| Interaction to Next Paint | At most 200 milliseconds at the 75th percentile | Field data and interaction traces |
| Cumulative Layout Shift | At most 0.1 | First load and navigation, including loading states |
| Local visual feedback | Within 100 milliseconds | Theme, hover, pressed and navigation affordances |
| Motion frame work | Within approximately 16 milliseconds for 60 Hz displays | Browser performance trace; animate opacity and transform |
| Search requests | One request after a 250–350 millisecond debounce | Request trace; cancel superseded operations |

Record browser, device, network latency, sample size, server configuration and percentile with each result. Test an Interactive Server session with 150 ms network round-trip latency and an interrupted connection as well as a local session. Server-dependent interactions have a network floor; keep purely presentational controls local when practical.

Keep prerendering for initial useful HTML. Avoid duplicate data queries during the prerender-to-interactive handoff by persisting only safe presentation state. Never persist secrets, private license contents or tenant credentials into browser markup. Use stable keys for repeated components and bounded pages or virtualization for large collections. Avoid external fonts, broad animation frameworks and chart libraries until a feature justifies their cost. Reserve layout space for pending content and cancel abandoned requests.

## Light and dark mode

- [ ] Theme colors are semantic CSS variables for surfaces, text, borders, focus, actions and status.
- [ ] Default follows the operating system; an explicit user choice persists across reloads and enhanced navigation.
- [ ] Theme is selected before first paint to avoid a light flash in dark mode.
- [ ] Normal text, including secondary text, meets 4.5:1 contrast in each theme; large text meets 3:1.
- [ ] Meaningful control boundaries, focus rings and icons meet 3:1 against adjacent surfaces.
- [ ] Hover, pressed, selected, disabled and error states remain distinguishable in both themes.
- [ ] Success, warning and failure use text or icon meaning as well as color.
- [ ] Theme control has a clear accessible name and exposes its current state.
- [ ] Storage being unavailable does not prevent startup or theme selection.

## Responsive and keyboard checks

- [ ] Check 375, 768, 1024 and 1440 CSS pixel widths, plus phone landscape.
- [ ] At 320 CSS pixels and at 200% browser zoom, content reflows without page-wide horizontal scrolling. Data tables may have a clearly contained scroll region.
- [ ] Primary touch controls target 44 by 44 CSS pixels where practical; WCAG exceptions must be deliberate.
- [ ] Navigation works with keyboard alone, has a visible focus ring and marks the current destination.
- [ ] A skip link reaches the main content; headings and landmark order match the visual structure.
- [ ] A mobile navigation disclosure announces expanded state and supports dismissal without losing focus.
- [ ] Sticky elements do not cover focused controls or validation messages.
- [ ] Forms use visible labels, inline errors and an error summary when needed; failed submissions retain input.
- [ ] Loading, empty, failure and disconnected states describe what the user can do next.

## Animation and presentation

- [ ] Use short, purposeful transitions: approximately 120 ms for feedback and 180–240 ms for entrances or disclosures.
- [ ] Respect `prefers-reduced-motion`; remove entrance movement, shimmer and nonessential animation.
- [ ] Animate transform and opacity instead of layout dimensions; avoid looping decorative motion.
- [ ] Do not shift the user's target during hover or press.
- [ ] Use consistent vector icons with accessible names on icon-only controls and hide decorative icons from assistive technology.
- [ ] Display business metrics only from actual data; identify demonstration or unavailable data explicitly.

## Verification record

Before production, record screenshots of both themes at desktop and mobile widths, a keyboard walkthrough, automated accessibility results plus manual contrast and screen-reader checks, and browser performance traces. No performance or accessibility completion claim should be made from a successful build alone.
