/** Temporary page body for routes nobody has built yet. Delete once every page exists. */
export function PlaceholderPage({ title, owner, todo }: { title: string; owner: 'A' | 'B'; todo: string }) {
  return (
    <section>
      <h1>{title}</h1>
      <p className="muted">
        Owner {owner} · TODO: {todo}
      </p>
    </section>
  )
}
