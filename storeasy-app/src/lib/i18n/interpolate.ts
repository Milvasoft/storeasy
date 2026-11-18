import { Fragment, createElement, type ReactNode } from 'react'

// "{count} members" + { count: 3 } -> "3 members". Replaces every occurrence (String.replace with a string pattern
// replaces only the first one).
export function interpolate(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in values ? String(values[name]) : match))
}

// The same with React nodes: "Sent to {email}" + { email: <b /> }.
export function withPlaceholders(template: string, nodes: Record<string, ReactNode>): ReactNode {
  return template.split(/(\{\w+\})/).map((part, index) => {
    const name = /^\{(\w+)\}$/.exec(part)?.[1]
    return createElement(Fragment, { key: index }, name && name in nodes ? nodes[name] : part)
  })
}
