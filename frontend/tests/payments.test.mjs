import test from 'node:test'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import ts from 'typescript'

const source = await readFile(new URL('../src/utils/payments.ts', import.meta.url), 'utf8')
const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } })
const { roundMoney, paymentAmounts, validPayment } = await import(`data:text/javascript;base64,${Buffer.from(outputText).toString('base64')}`)

test('cash and card payments allocate the full total', () => {
  assert.deepEqual(paymentAmounts('Cash', 10, ''), { cashAmount: 10, cardAmount: 0 })
  assert.deepEqual(paymentAmounts('Card', 10, ''), { cashAmount: 0, cardAmount: 10 })
})
test('mixed payments calculate a three-decimal card remainder', () => {
  assert.deepEqual(paymentAmounts('Mixed', 0.3, '0.1'), { cashAmount: 0.1, cardAmount: 0.2 })
  assert.equal(validPayment('Mixed', 0.3, '0.1'), true)
})
test('invalid or incomplete splits cannot be submitted', () => {
  for (const cash of ['', '0', '-1', '10', '11', 'not-a-number', '4.0001']) {
    assert.equal(validPayment('Mixed', 10, cash), false, cash)
  }
})
test('rounding matches API midpoint-to-even behavior', () => {
  assert.equal(roundMoney(0.0625), 0.062)
  assert.equal(roundMoney(0.0635), 0.064)
  assert.equal(roundMoney(-0.0625), -0.062)
  assert.equal(roundMoney(0.1 + 0.2), 0.3)
})
