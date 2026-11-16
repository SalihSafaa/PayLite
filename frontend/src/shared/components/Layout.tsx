import { AppShell, Burger, Button, Drawer, Group, Stack, Text, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { Link, Outlet, useMatch } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { NotificationBell } from '../../features/activity/NotificationBell'

function NavItem({ to, end = false, onClick, children }: { to: string; end?: boolean; onClick?: () => void; children: string }) {
  const active = useMatch({ path: to, end }) !== null
  return (
    <Button
      component={Link}
      to={to}
      onClick={onClick}
      variant={active ? 'light' : 'subtle'}
      size="compact-md"
      justify="start"
      aria-current={active ? 'page' : undefined}
    >
      {children}
    </Button>
  )
}

function NavLinks({ isMerchant, onNavigate }: { isMerchant: boolean; onNavigate?: () => void }) {
  return (
    <>
      {/* Owner A */}
      <NavItem to="/" end onClick={onNavigate}>Wallet</NavItem>
      <NavItem to="/send" onClick={onNavigate}>Send money</NavItem>
      <NavItem to="/history" onClick={onNavigate}>History</NavItem>
      {/* Owner B */}
      <NavItem to="/notifications" onClick={onNavigate}>Notifications</NavItem>
      {isMerchant && <NavItem to="/merchant/payments" onClick={onNavigate}>Payments</NavItem>}
      {isMerchant && <NavItem to="/merchant/summary" onClick={onNavigate}>Daily summary</NavItem>}
    </>
  )
}

export function Layout() {
  const { user, signOut } = useAuth()
  const isMerchant = user?.role === 'Merchant'
  const [menuOpen, menu] = useDisclosure(false)

  return (
    <AppShell header={{ height: 60 }} padding="md">
      <AppShell.Header px="md">
        <Group h="100%" justify="space-between" wrap="nowrap">
          <Group gap="lg" wrap="nowrap">
            {user && <Burger opened={menuOpen} onClick={menu.toggle} hiddenFrom="md" size="sm" aria-label="Open menu" />}
            <Title order={3} c="blue.7">PayLite</Title>
            {user && (
              <Group component="nav" gap={4} wrap="nowrap" visibleFrom="md">
                <NavLinks isMerchant={isMerchant} />
              </Group>
            )}
          </Group>
          {user && (
            <Group gap="sm" wrap="nowrap">
              <NotificationBell />
              <Text size="sm" c="dimmed" visibleFrom="sm">{user.username}</Text>
              <Button variant="default" size="compact-md" onClick={signOut}>Log out</Button>
            </Group>
          )}
        </Group>
      </AppShell.Header>

      {/* Small screens: the same links in a drawer. */}
      <Drawer opened={menuOpen} onClose={menu.close} size="xs" title="PayLite" hiddenFrom="md">
        <Stack component="nav" gap={4}>
          <NavLinks isMerchant={isMerchant} onNavigate={menu.close} />
        </Stack>
      </Drawer>

      <AppShell.Main bg="gray.0">
        <div className="page">
          <Outlet />
        </div>
      </AppShell.Main>
    </AppShell>
  )
}
