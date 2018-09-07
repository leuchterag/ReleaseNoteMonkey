# Release Notes

{{#each versions}}

## {{@Version.Original}}
  {{#if @Notes}}
    {{#each @Notes}}
  - DE: {{@Data.DE}}
    EN: {{@Data.EN}}
    {{/each}}
  {{else}}
No Release Notes available.
  {{/if}}
{{/each}}