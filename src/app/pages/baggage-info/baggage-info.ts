import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-baggage-info',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './baggage-info.html',
  styleUrl: './baggage-info.scss'
})
export class BaggageInfoComponent {
  classes = [
    {
      name: 'Economy',
      icon: 'bi-person',
      color: 'blue',
      cabinBaggage: '7 kg',
      checkInBaggage: '15 kg',
      extraBaggageRate: '₹500 per kg',
      oversizeRate: '₹1000',
      features: [
        '1 cabin bag up to 7 kg',
        '1 check-in bag up to 15 kg',
        'Extra baggage at ₹500/kg',
        'Sports equipment allowed'
      ]
    },
    {
      name: 'Business',
      icon: 'bi-briefcase',
      color: 'gold',
      cabinBaggage: '10 kg',
      checkInBaggage: '25 kg',
      extraBaggageRate: '₹300 per kg',
      oversizeRate: '₹800',
      features: [
        '2 cabin bags up to 10 kg total',
        '2 check-in bags up to 25 kg total',
        'Extra baggage at ₹300/kg',
        'Priority baggage handling',
        'Sports equipment allowed'
      ]
    }
  ];

  prohibited = [
    { icon: 'bi-fire', item: 'Flammable items', desc: 'Lighters, matches, petrol' },
    { icon: 'bi-radioactive', item: 'Explosives', desc: 'Fireworks, ammunition' },
    { icon: 'bi-bandaid', item: 'Sharp objects', desc: 'Knives, scissors in cabin' },
    { icon: 'bi-battery-half', item: 'Lithium batteries', desc: 'Above 100Wh in hold' },
    { icon: 'bi-droplet', item: 'Liquids over 100ml', desc: 'In cabin baggage only' },
    { icon: 'bi-wind', item: 'Compressed gas', desc: 'Aerosols, gas cylinders' }
  ];

  tips = [
    'Label your baggage with name, address and phone number',
    'Remove old airline tags from your bags',
    'Do not pack valuables in check-in baggage',
    'Arrive at check-in counter 2 hours before departure',
    'Baggage wrapping service available at airport',
    'Keep medicine and essentials in cabin baggage'
  ];
  selectedClass = 'Economy';
extraWeight = 0;
calculatedCost = 0;

calculateCost() {
  const rate = this.selectedClass === 'Business' ? 300 : 500;
  this.calculatedCost = this.extraWeight * rate;
}
}